using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using Xunit;

namespace DearStory.Protocol.E2ETests;

public sealed class CrossLanguageHandshakeTests
{
    [Fact(Timeout = 30_000)]
    public async Task DotNet_client_negotiates_with_native_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartNative("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunManagedAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "control.handshake.v1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WELCOME protocol=1.0", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, await server.WaitForExitAsync());
    }

    [Fact(Timeout = 30_000)]
    public async Task DotNet_client_reports_major_mismatch_from_native_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartNative("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunManagedAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--protocol-major", "2",
            "--require", "control.handshake.v1");

        Assert.Equal(22, result.ExitCode);
        Assert.Contains("REJECT code=protocol.major_mismatch", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(22, await server.WaitForExitAsync());
    }

    [Fact(Timeout = 30_000)]
    public async Task DotNet_client_reports_missing_required_capability_from_native_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartNative("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunManagedAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "visual.snapshot");

        Assert.Equal(22, result.ExitCode);
        Assert.Contains("REJECT code=protocol.required_capability_missing", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(22, await server.WaitForExitAsync());
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_client_negotiates_with_managed_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartManaged("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunNativeAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "control.handshake.v1");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WELCOME protocol=1.0", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, await server.WaitForExitAsync());
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_client_reports_major_mismatch_from_managed_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartManaged("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunNativeAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--protocol-major", "2",
            "--require", "control.handshake.v1");

        Assert.Equal(22, result.ExitCode);
        Assert.Contains("REJECT code=protocol.major_mismatch", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(22, await server.WaitForExitAsync());
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_client_reports_missing_required_capability_from_managed_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartManaged("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunNativeAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "visual.snapshot");

        Assert.Equal(22, result.ExitCode);
        Assert.Contains("REJECT code=protocol.required_capability_missing", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(22, await server.WaitForExitAsync());
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_server_rejects_malformed_json_frame()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartNative("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);
        await SendFramedPayloadAsync(pipeName, """{ definitely not json }"""u8.ToArray(), cancellationToken);

        Assert.Equal(22, await server.WaitForExitAsync());
        var stderr = await server.StandardErrorAsync;
        Assert.Contains("protocol.invalid_envelope", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Managed_server_rejects_malformed_json_frame()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartManaged("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);
        await SendFramedPayloadAsync(pipeName, """{ definitely not json }"""u8.ToArray(), cancellationToken);

        Assert.Equal(22, await server.WaitForExitAsync());
        var stderr = await server.StandardErrorAsync;
        Assert.Contains("protocol.invalid_envelope", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_server_rejects_oversize_prefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartNative("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);
        await SendOversizePrefixAsync(pipeName, cancellationToken);

        Assert.Equal(21, await server.WaitForExitAsync());
        var stderr = await server.StandardErrorAsync;
        Assert.Contains("protocol.frame_too_large", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Managed_server_rejects_oversize_prefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        await using var server = ProcessProbe.StartManaged("serve", "--pipe", pipeName, "--once");
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);
        await SendOversizePrefixAsync(pipeName, cancellationToken);

        Assert.Equal(21, await server.WaitForExitAsync());
        var stderr = await server.StandardErrorAsync;
        Assert.Contains("protocol.frame_too_large", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task DotNet_client_reports_server_exit_before_welcome()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        var serverTask = RunClosingRawServerAsync(pipeName, cancellationToken);
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunManagedAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "control.handshake.v1");

        Assert.Equal(21, result.ExitCode);
        Assert.Contains("protocol.pipe_closed", result.StandardError, StringComparison.Ordinal);
        await serverTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_client_reports_server_exit_before_welcome()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        var serverTask = RunClosingRawServerAsync(pipeName, cancellationToken);
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunNativeAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "control.handshake.v1");

        Assert.Equal(21, result.ExitCode);
        Assert.Contains("protocol.pipe_closed", result.StandardError, StringComparison.Ordinal);
        await serverTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task DotNet_client_reports_timeout_when_server_never_replies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        var serverTask = RunSilentRawServerAsync(pipeName, cancellationToken);
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunManagedAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "control.handshake.v1");

        Assert.Equal(23, result.ExitCode);
        Assert.Contains("protocol.operation_cancelled", result.StandardError, StringComparison.Ordinal);
        await serverTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Native_client_reports_timeout_when_server_never_replies()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-e2e-{Guid.NewGuid():N}";
        var serverTask = RunSilentRawServerAsync(pipeName, cancellationToken);
        await ProcessProbe.WaitForPipeAsync(pipeName, cancellationToken);

        var result = await ProcessProbe.RunNativeAsync(
            "connect", "--pipe", pipeName, "--role", "host",
            "--require", "control.handshake.v1");

        Assert.Equal(23, result.ExitCode);
        Assert.Contains("protocol.operation_cancelled", result.StandardError, StringComparison.Ordinal);
        await serverTask;
    }

    private static async Task SendFramedPayloadAsync(string pipeName, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellationToken);
        await LengthPrefixedControlStream.WriteAsync(client, payload, cancellationToken);
    }

    private static async Task SendOversizePrefixAsync(string pipeName, CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellationToken);
        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, LengthPrefixedControlStream.MaxFrameBytes + 1U);
        await client.WriteAsync(prefix, cancellationToken);
    }

    private static async Task RunClosingRawServerAsync(string pipeName, CancellationToken cancellationToken)
    {
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(cancellationToken);
        _ = await LengthPrefixedControlStream.ReadAsync(server, cancellationToken);
    }

    private static async Task RunSilentRawServerAsync(string pipeName, CancellationToken cancellationToken)
    {
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(cancellationToken);
        _ = await LengthPrefixedControlStream.ReadAsync(server, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
    }
}
