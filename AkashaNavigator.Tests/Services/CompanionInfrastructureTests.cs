using System.Buffers.Binary;
using System.Text.Json;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using AkashaNavigator.Services.Companion;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public class CompanionInfrastructureTests
{
    [Fact]
    public void ResolveExecutable_ShouldAcceptExistingFileUnderPluginRoot()
    {
        using var directory = new TemporaryDirectory();
        var workerDirectory = Path.Combine(directory.Path, "worker", "win-x64");
        Directory.CreateDirectory(workerDirectory);
        var executable = Path.Combine(workerDirectory, "Worker.exe");
        File.WriteAllBytes(executable, []);

        var result = CompanionPathValidator.ResolveExecutable(
            directory.Path,
            "worker/win-x64/Worker.exe");

        Assert.Equal(executable, result, ignoreCase: true);
    }

    [Theory]
    [InlineData("../outside.exe")]
    [InlineData("C:/outside.exe")]
    [InlineData("worker/not-an-exe.dll")]
    public void ResolveExecutable_ShouldRejectUnsafePath(string relativePath)
    {
        using var directory = new TemporaryDirectory();

        Assert.ThrowsAny<Exception>(() =>
            CompanionPathValidator.ResolveExecutable(directory.Path, relativePath));
    }

    [Fact]
    public void ResolveExecutable_ShouldRejectJunctionInPluginPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var pluginDirectory = new TemporaryDirectory();
        using var outsideDirectory = new TemporaryDirectory();
        var executable = Path.Combine(outsideDirectory.Path, "Worker.exe");
        File.WriteAllBytes(executable, []);
        var junction = Path.Combine(pluginDirectory.Path, "worker");
        var commandInterpreter = Environment.GetEnvironmentVariable("ComSpec")!;
        using var createJunction = Process.Start(new ProcessStartInfo
        {
            FileName = commandInterpreter,
            Arguments = $"/d /c mklink /J \"{junction}\" \"{outsideDirectory.Path}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        Assert.NotNull(createJunction);
        createJunction!.WaitForExit();
        Assert.True(createJunction.ExitCode == 0, createJunction.StandardError.ReadToEnd());

        try
        {
            Assert.Throws<InvalidDataException>(() =>
                CompanionPathValidator.ResolveExecutable(pluginDirectory.Path, "worker/Worker.exe"));
        }
        finally
        {
            if (Directory.Exists(junction))
                Directory.Delete(junction);
        }
    }

    [Fact]
    public async Task JobObject_DisposeShouldTerminateAssignedProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var commandInterpreter = Environment.GetEnvironmentVariable("ComSpec")!;
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = commandInterpreter,
            Arguments = "/d /c ping 127.0.0.1 -n 60 > nul",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Assert.NotNull(process);
        using var job = CompanionJobObject.Create();
        job.AssignProcess(process!.SafeHandle);

        job.Dispose();

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task Framing_ShouldRoundTripCompatibleEnvelope()
    {
        var framing = new CompanionFraming();
        await using var stream = new MemoryStream();
        var request = new CompanionEnvelope
        {
            Type = CompanionProtocol.Request,
            CorrelationId = "request-1",
            Method = "worker.echo",
            Payload = JsonSerializer.SerializeToElement(new { value = "echo" })
        };

        await framing.WriteAsync(stream, request);
        stream.Position = 0;
        var result = await framing.ReadAsync(stream);

        Assert.Equal(CompanionProtocol.Request, result.Type);
        Assert.Equal("request-1", result.CorrelationId);
        Assert.Equal("echo", result.Payload!.Value.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Framing_ShouldRejectOversizedIncomingFrame()
    {
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, CompanionProtocol.MaximumPayloadBytes + 1);
        await using var stream = new MemoryStream(header);
        var framing = new CompanionFraming();

        await Assert.ThrowsAsync<InvalidDataException>(async () => await framing.ReadAsync(stream));
    }

    [Fact]
    public async Task RequestMultiplexer_ShouldDeliverEmergencyStopWhileEarlierRequestIsPending()
    {
        var pipeName = $"akasha-multiplexer-{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverConnection = server.WaitForConnectionAsync(timeout.Token);
        await client.ConnectAsync(timeout.Token);
        await serverConnection;

        var framing = new CompanionFraming();
        var multiplexer = new CompanionRequestMultiplexer(
            server,
            framing,
            TimeSpan.FromSeconds(5),
            maximumPendingRequests: 2);
        var longRequestReceived = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLongResponse = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var workerTask = Task.Run(
            async () =>
            {
                var longRequest = await framing.ReadAsync(client, timeout.Token);
                Assert.Equal("features.test.block", longRequest.Method);
                longRequestReceived.TrySetResult();

                var emergencyRequest = await framing.ReadAsync(client, timeout.Token);
                Assert.Equal("automation.emergencyStop", emergencyRequest.Method);
                await framing.WriteAsync(
                    client,
                    Response(
                        emergencyRequest,
                        JsonSerializer.SerializeToElement(new { active = true })),
                    timeout.Token);

                await releaseLongResponse.Task.WaitAsync(timeout.Token);
                await framing.WriteAsync(
                    client,
                    Response(
                        longRequest,
                        JsonSerializer.SerializeToElement(new { completed = true })),
                    timeout.Token);
            },
            timeout.Token);

        try
        {
            var longInvocation = multiplexer.InvokeAsync(
                "features.test.block",
                null,
                timeout.Token);
            await longRequestReceived.Task.WaitAsync(timeout.Token);

            var capacityError = await Assert.ThrowsAsync<InvalidOperationException>(
                () => multiplexer.InvokeAsync(
                    "features.test.overflow",
                    null,
                    timeout.Token));
            Assert.Contains("capacity", capacityError.Message, StringComparison.OrdinalIgnoreCase);

            var emergency = await multiplexer.InvokeAsync(
                "automation.emergencyStop",
                null,
                timeout.Token);

            Assert.True(emergency!.Value.GetProperty("active").GetBoolean());
            Assert.False(longInvocation.IsCompleted);
            releaseLongResponse.TrySetResult();
            var longResult = await longInvocation;
            Assert.True(longResult!.Value.GetProperty("completed").GetBoolean());
            await workerTask;
        }
        finally
        {
            releaseLongResponse.TrySetResult();
            multiplexer.RequestStop();
            await multiplexer.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void HandshakeValidation_ShouldRejectMissingMessageType()
    {
        var token = "0123456789abcdef0123456789abcdef";
        var hello = new CompanionEnvelope
        {
            Type = null!,
            ProtocolVersion = CompanionProtocol.CurrentVersion,
            ParentProcessId = Environment.ProcessId,
            Token = token
        };

        Assert.False(CompanionProcessManager.IsValidHello(
            hello,
            token,
            CompanionProtocol.CurrentVersion));
    }

    [Fact]
    public void HandshakeValidation_ShouldRejectWrongTokenWithValidLength()
    {
        var expectedToken = "0123456789abcdef0123456789abcdef";
        var hello = new CompanionEnvelope
        {
            Type = CompanionProtocol.Hello,
            ProtocolVersion = CompanionProtocol.CurrentVersion,
            ParentProcessId = Environment.ProcessId,
            Token = "fedcba9876543210fedcba9876543210"
        };

        Assert.False(CompanionProcessManager.IsValidHello(
            hello,
            expectedToken,
            CompanionProtocol.CurrentVersion));
    }

    [Fact]
    public void ConfigurePluginEnvironment_ShouldPassPluginResourceDirectory()
    {
        var startInfo = new ProcessStartInfo();

        CompanionProcessManager.ConfigurePluginEnvironment(
            startInfo,
            AppConstants.AutomationPluginId);

        Assert.Equal(
            AkashaNavigator.Helpers.AppPaths.GetPluginResourceDirectory(
                AppConstants.AutomationPluginId),
            startInfo.Environment[AppConstants.PluginDataDirectoryEnvironmentVariable]);
    }

    [Fact]
    public async Task Dispose_ShouldWaitForInFlightStartBeforeCompleting()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "worker.exe");
        File.Copy(Environment.GetEnvironmentVariable("ComSpec")!, executable);
        var manager = new CompanionProcessManager(new TestLogService());
        using var cancellation = new CancellationTokenSource();
        var startTask = manager.StartAsync(
            "dispose-race",
            directory.Path,
            new CompanionManifest { Executable = "worker.exe" },
            cancellation.Token);

        await Task.Delay(100);
        Assert.False(startTask.IsCompleted);
        var disposeTask = Task.Run(manager.Dispose);
        await Task.Delay(100);

        var disposeCompletedBeforeStartFinished = disposeTask.IsCompleted;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<Exception>(async () => await startTask);
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(disposeCompletedBeforeStartFinished);
        Assert.False(manager.GetStatus("dispose-race").Running);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"AkashaNavigator.CompanionTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static CompanionEnvelope Response(
        CompanionEnvelope request,
        JsonElement payload) =>
        new()
        {
            Type = CompanionProtocol.Response,
            CorrelationId = request.CorrelationId,
            Payload = payload
        };

    private sealed class TestLogService : AkashaNavigator.Core.Interfaces.ILogService
    {
        public string LogDirectory => string.Empty;

        public void Debug(string source, string message) { }
        public void Debug(string source, string template, params object?[] args) { }
        public void Info(string source, string message) { }
        public void Info(string source, string template, params object?[] args) { }
        public void Warn(string source, string message) { }
        public void Warn(string source, string template, params object?[] args) { }
        public void Error(string source, string message) { }
        public void Error(string source, string template, params object?[] args) { }
        public void Error(string source, Exception ex, string template, params object?[] args) { }
    }
}
