using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services.Companion;

namespace AkashaNavigator.Services;

public sealed class CompanionProcessManager : ICompanionProcessManager
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogService _logService;
    private readonly ConcurrentDictionary<string, CompanionSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CompanionFraming _framing = new();
    private int _disposed;

    public CompanionProcessManager(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public async Task<CompanionStatus> StartAsync(
        string pluginId,
        string pluginDirectory,
        CompanionManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(manifest);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (_sessions.TryGetValue(pluginId, out var existing))
            {
                if (existing.IsRunning)
                {
                    return existing.GetStatus();
                }

                _sessions.TryRemove(pluginId, out _);
                await existing.DisposeAsync().ConfigureAwait(false);
            }

            var executable = CompanionPathValidator.ResolveExecutable(
                pluginDirectory,
                manifest.Executable ?? string.Empty);
            var session = await CreateSessionAsync(
                    pluginId,
                    executable,
                    manifest.ProtocolVersion,
                    manifest.ShutdownTimeoutMs,
                    cancellationToken)
                .ConfigureAwait(false);

            if (Volatile.Read(ref _disposed) != 0)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                ThrowIfDisposed();
            }

            if (!_sessions.TryAdd(pluginId, session))
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException("A companion session already exists for this plugin.");
            }

            _logService.Info(nameof(CompanionProcessManager),
                             "Started companion for plugin {PluginId} with process {ProcessId}",
                             pluginId, session.GetStatus().ProcessId);
            return session.GetStatus();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task<JsonElement?> InvokeAsync(
        string pluginId,
        string method,
        JsonElement? payload,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_sessions.TryGetValue(pluginId, out var session) || !session.IsRunning)
        {
            throw new InvalidOperationException("Companion process is not running.");
        }

        return session.InvokeAsync(method, payload, cancellationToken);
    }

    public CompanionStatus GetStatus(string pluginId)
    {
        if (_sessions.TryGetValue(pluginId, out var session))
        {
            return session.GetStatus();
        }

        return new CompanionStatus(false, "stopped");
    }

    public async Task StopAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            await StopSessionCoreAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await StopAllSessionsCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifecycleGate.Wait();
        try
        {
            StopAllSessionsCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StopAllSessionsCoreAsync(CancellationToken cancellationToken)
    {
        foreach (var pluginId in _sessions.Keys.ToArray())
        {
            await StopSessionCoreAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StopSessionCoreAsync(string pluginId, CancellationToken cancellationToken)
    {
        if (_sessions.TryRemove(pluginId, out var session))
        {
            await session.StopAsync(cancellationToken).ConfigureAwait(false);
            _logService.Info(nameof(CompanionProcessManager),
                             "Stopped companion for plugin {PluginId}", pluginId);
        }
    }

    private async Task<CompanionSession> CreateSessionAsync(
        string pluginId,
        string executable,
        int protocolVersion,
        int shutdownTimeoutMs,
        CancellationToken cancellationToken)
    {
        var pipeName = $"akasha-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var token = CreateSessionToken();
        NamedPipeServerStream? pipe = null;
        Process? process = null;
        CompanionJobObject? jobObject = null;

        try
        {
            pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(pipeName);
            startInfo.ArgumentList.Add("--token");
            startInfo.ArgumentList.Add(token);
            startInfo.ArgumentList.Add("--parent-pid");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add("--protocol-version");
            startInfo.ArgumentList.Add(protocolVersion.ToString());
            ConfigurePluginEnvironment(startInfo, pluginId);

            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new InvalidOperationException("Companion process could not be started.");
            }

            jobObject = CompanionJobObject.Create();
            jobObject.AssignProcess(process.SafeHandle);

            using (var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectionCancellation.CancelAfter(ConnectionTimeout);
                await pipe.WaitForConnectionAsync(connectionCancellation.Token).ConfigureAwait(false);
            }

            using (var handshakeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                handshakeCancellation.CancelAfter(HandshakeTimeout);
                var hello = await _framing.ReadAsync(pipe, handshakeCancellation.Token).ConfigureAwait(false);
                var accepted = IsValidHello(hello, token, protocolVersion);
                await _framing.WriteAsync(
                    pipe,
                    new CompanionEnvelope
                    {
                        Type = CompanionProtocol.Welcome,
                        ProtocolVersion = CompanionProtocol.CurrentVersion,
                        Accepted = accepted,
                        Error = accepted ? null : new CompanionError("invalid_handshake", "Companion handshake rejected.")
                    },
                    handshakeCancellation.Token).ConfigureAwait(false);

                if (!accepted)
                {
                    throw new InvalidDataException("Companion handshake was rejected.");
                }
            }

            var session = new CompanionSession(
                pluginId,
                process,
                pipe,
                jobObject,
                _framing,
                TimeSpan.FromMilliseconds(shutdownTimeoutMs));
            process = null;
            pipe = null;
            jobObject = null;
            return session;
        }
        catch
        {
            pipe?.Dispose();
            jobObject?.Dispose();
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }

                process.Dispose();
            }

            throw;
        }
    }

    internal static bool IsValidHello(CompanionEnvelope hello, string expectedToken, int protocolVersion)
    {
        if (!string.Equals(hello.Type, CompanionProtocol.Hello, StringComparison.Ordinal) ||
            hello.ProtocolVersion != protocolVersion ||
            hello.ParentProcessId != Environment.ProcessId ||
            string.IsNullOrEmpty(hello.Token))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var actualBytes = Encoding.UTF8.GetBytes(hello.Token);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    internal static void ConfigurePluginEnvironment(ProcessStartInfo startInfo, string pluginId)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        startInfo.Environment[AppConstants.PluginDataDirectoryEnvironmentVariable] =
            AppPaths.GetPluginResourceDirectory(pluginId);
    }

    private static string CreateSessionToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
