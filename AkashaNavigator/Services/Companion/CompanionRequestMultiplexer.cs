using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace AkashaNavigator.Services.Companion;

internal sealed class CompanionRequestMultiplexer
{
    private static readonly TimeSpan DefaultInvocationTimeout = TimeSpan.FromSeconds(10);
    private const int DefaultMaximumPendingRequests = 64;

    private readonly Stream _stream;
    private readonly CompanionFraming _framing;
    private readonly TimeSpan _invocationTimeout;
    private readonly int _maximumPendingRequests;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CompanionEnvelope>> _pending = new();
    private readonly Task _readerTask;
    private int _faulted;
    private int _pendingCount;
    private int _stopping;

    public CompanionRequestMultiplexer(
        Stream stream,
        CompanionFraming framing,
        TimeSpan? invocationTimeout = null,
        int maximumPendingRequests = DefaultMaximumPendingRequests)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _framing = framing ?? throw new ArgumentNullException(nameof(framing));
        _invocationTimeout = invocationTimeout ?? DefaultInvocationTimeout;
        _maximumPendingRequests = maximumPendingRequests > 1
            ? maximumPendingRequests
            : throw new ArgumentOutOfRangeException(nameof(maximumPendingRequests));
        _readerTask = ReadResponsesAsync();
    }

    public bool IsAvailable =>
        Volatile.Read(ref _stopping) == 0 && Volatile.Read(ref _faulted) == 0;

    public async Task<JsonElement?> InvokeAsync(
        string method,
        JsonElement? payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Companion transport is not available.");
        }

        if (!TryAcquirePendingSlot(IsSafetyControlMethod(method)))
        {
            throw new InvalidOperationException("Companion request capacity is exhausted.");
        }

        try
        {
            using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);
            invocationCancellation.CancelAfter(_invocationTimeout);
            var correlationId = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<CompanionEnvelope>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(correlationId, completion))
            {
                throw new InvalidOperationException("A duplicate companion correlation ID was generated.");
            }

            try
            {
                await _writeGate.WaitAsync(invocationCancellation.Token).ConfigureAwait(false);
                try
                {
                    if (!IsAvailable)
                    {
                        throw new InvalidOperationException("Companion transport is not available.");
                    }

                    await _framing.WriteAsync(
                        _stream,
                        new CompanionEnvelope
                        {
                            Type = CompanionProtocol.Request,
                            CorrelationId = correlationId,
                            Method = method,
                            Payload = payload?.Clone()
                        },
                        invocationCancellation.Token).ConfigureAwait(false);
                }
                finally
                {
                    _writeGate.Release();
                }

                var response = await completion.Task
                    .WaitAsync(invocationCancellation.Token)
                    .ConfigureAwait(false);
                if (response.Error != null)
                {
                    throw new InvalidOperationException($"{response.Error.Code}: {response.Error.Message}");
                }

                return response.Payload?.Clone();
            }
            finally
            {
                _pending.TryRemove(correlationId, out _);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    private bool TryAcquirePendingSlot(bool safetyControl)
    {
        var limit = safetyControl
            ? _maximumPendingRequests
            : _maximumPendingRequests - 1;

        while (true)
        {
            var current = Volatile.Read(ref _pendingCount);
            if (current >= limit)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _pendingCount, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    private static bool IsSafetyControlMethod(string method) =>
        method.Equals("automation.emergencyStop", StringComparison.Ordinal) ||
        method.Equals("worker.shutdown", StringComparison.Ordinal);

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        FailAllPending(new OperationCanceledException("The companion session is stopping."));
    }

    public async Task WaitForCompletionAsync()
    {
        try
        {
            await _readerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (Volatile.Read(ref _stopping) != 0)
        {
        }
    }

    private async Task ReadResponsesAsync()
    {
        try
        {
            while (!_lifetimeCancellation.IsCancellationRequested)
            {
                var response = await _framing.ReadAsync(
                    _stream,
                    _lifetimeCancellation.Token).ConfigureAwait(false);
                if (!string.Equals(response.Type, CompanionProtocol.Response, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(response.CorrelationId))
                {
                    throw new InvalidDataException(
                        "Companion response type or correlation ID is invalid.");
                }

                if (_pending.TryRemove(response.CorrelationId, out var completion))
                {
                    completion.TrySetResult(response);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception) when (Volatile.Read(ref _stopping) != 0)
        {
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref _faulted, 1);
            FailAllPending(exception);
        }
    }

    private void FailAllPending(Exception exception)
    {
        foreach (var pending in _pending)
        {
            if (_pending.TryRemove(pending.Key, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }
}
