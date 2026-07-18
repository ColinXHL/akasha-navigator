using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Services.Companion;

internal sealed class CompanionSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly NamedPipeServerStream _pipe;
    private readonly CompanionJobObject _jobObject;
    private readonly CompanionRequestMultiplexer _requests;
    private readonly TimeSpan _gracefulShutdownTimeout;
    private int _stopping;

    public CompanionSession(
        string pluginId,
        Process process,
        NamedPipeServerStream pipe,
        CompanionJobObject jobObject,
        CompanionFraming framing,
        TimeSpan gracefulShutdownTimeout)
    {
        PluginId = pluginId;
        _process = process;
        _pipe = pipe;
        _jobObject = jobObject;
        _requests = new CompanionRequestMultiplexer(pipe, framing);
        _gracefulShutdownTimeout = gracefulShutdownTimeout;
    }

    public string PluginId { get; }

    public bool IsRunning
    {
        get
        {
            try
            {
                return Volatile.Read(ref _stopping) == 0 &&
                       _requests.IsAvailable &&
                       !_process.HasExited &&
                       _pipe.IsConnected;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public CompanionStatus GetStatus() => IsRunning
        ? new CompanionStatus(true, "running", _process.Id)
        : new CompanionStatus(false, Volatile.Read(ref _stopping) == 0 ? "exited" : "stopped");

    public Task<JsonElement?> InvokeAsync(
        string method,
        JsonElement? payload,
        CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Companion process is not running.");
        }

        return _requests.InvokeAsync(method, payload, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
        {
            return;
        }

        var shutdownStopwatch = Stopwatch.StartNew();
        try
        {
            using var shutdownCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdownCancellation.CancelAfter(_gracefulShutdownTimeout);

            try
            {
                if (!_process.HasExited && _pipe.IsConnected && _requests.IsAvailable)
                {
                    _ = await _requests.InvokeAsync(
                        "worker.shutdown",
                        null,
                        shutdownCancellation.Token).ConfigureAwait(false);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Forced cleanup below is the reliability boundary.
            }
        }
        finally
        {
            _requests.RequestStop();
            await _pipe.DisposeAsync().ConfigureAwait(false);
            await _requests.WaitForCompletionAsync().ConfigureAwait(false);

            try
            {
                if (!_process.HasExited)
                {
                    var remaining =
                        _gracefulShutdownTimeout -
                        shutdownStopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        TryKillProcessTree();
                    }
                    else
                    {
                        using var exitCancellation =
                            new CancellationTokenSource(remaining);
                        await _process.WaitForExitAsync(exitCancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TryKillProcessTree();
            }
            catch (InvalidOperationException)
            {
            }

            if (!_process.HasExited)
            {
                TryKillProcessTree();
            }

            _jobObject.Dispose();
            _process.Dispose();
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private void TryKillProcessTree()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(
                (int)_gracefulShutdownTimeout.TotalMilliseconds);
        }
        catch
        {
            // Closing the Job Object remains the final kill-on-close guarantee.
        }
    }
}
