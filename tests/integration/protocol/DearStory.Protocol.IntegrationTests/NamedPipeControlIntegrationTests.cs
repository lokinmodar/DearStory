using System.Buffers.Binary;
using System.IO.Pipes;
using DearStory.Protocol.Generated;
using DearStory.Transport.Windows;
using Xunit;

namespace DearStory.Protocol.IntegrationTests;

public sealed class NamedPipeControlIntegrationTests
{
    [Fact(Timeout = 10_000)]
    public async Task Server_and_client_exchange_two_frames_in_order()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeControlServer(pipeName);
        var acceptTask = server.AcceptAsync(cancellationToken).AsTask();
        await using var client = await NamedPipeControlClient.ConnectAsync(pipeName, cancellationToken);
        await using var serverConnection = await acceptTask;

        var writeTask = Task.Run(async () =>
        {
            await client.WriteAsync(ControlCodec.Encode(CreateHelloEnvelope(Guid.Parse("01010101-0101-4101-8101-010101010101"))), cancellationToken);
            await client.WriteAsync(ControlCodec.Encode(CreateHelloEnvelope(Guid.Parse("02020202-0202-4202-8202-020202020202"))), cancellationToken);
        }, cancellationToken);

        var firstFrame = await serverConnection.ReadAsync(cancellationToken);
        var secondFrame = await serverConnection.ReadAsync(cancellationToken);
        await writeTask;

        Assert.NotNull(firstFrame);
        Assert.NotNull(secondFrame);

        var firstEnvelope = ControlCodec.Decode(firstFrame!);
        var secondEnvelope = ControlCodec.Decode(secondFrame!);
        Assert.True(firstEnvelope.IsSuccess, firstEnvelope.Error?.Message);
        Assert.True(secondEnvelope.IsSuccess, secondEnvelope.Error?.Message);
        Assert.Equal(Guid.Parse("01010101-0101-4101-8101-010101010101"), firstEnvelope.Value!.MessageId);
        Assert.Equal(Guid.Parse("02020202-0202-4202-8202-020202020202"), secondEnvelope.Value!.MessageId);
    }

    [Fact(Timeout = 10_000)]
    public async Task Client_connect_can_be_canceled_before_a_server_starts()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => NamedPipeControlClient.ConnectAsync($"dearstory-test-{Guid.NewGuid():N}", cancellation.Token).AsTask());
    }

    [Fact(Timeout = 10_000)]
    public async Task Server_read_rejects_peer_disconnect_mid_frame()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeControlServer(pipeName);
        var acceptTask = server.AcceptAsync(cancellationToken).AsTask();

        await using var rawClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await rawClient.ConnectAsync(cancellationToken);
        await using var serverConnection = await acceptTask;
        var readTask = serverConnection.ReadAsync(cancellationToken).AsTask();

        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, 32);
        await rawClient.WriteAsync(prefix, cancellationToken);
        await rawClient.WriteAsync("""{"type":"hello"}"""u8.ToArray().AsMemory(0, 8), cancellationToken);
        await rawClient.DisposeAsync();

        var error = await Assert.ThrowsAsync<ProtocolException>(
            async () => await readTask);

        Assert.Equal("protocol.invalid_envelope", error.Code);
    }

    [Fact(Timeout = 10_000)]
    public async Task Server_accepts_exactly_one_client_per_instance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeControlServer(pipeName);
        var acceptTask = server.AcceptAsync(cancellationToken).AsTask();
        await using var firstClient = await NamedPipeControlClient.ConnectAsync(pipeName, cancellationToken);
        await using var serverConnection = await acceptTask;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => NamedPipeControlClient.ConnectAsync(pipeName, cancellation.Token).AsTask());

        _ = serverConnection;
    }

    [Fact(Timeout = 10_000)]
    public async Task Server_rejects_a_second_accept_after_transferring_the_first_connection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"dearstory-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeControlServer(pipeName);
        var firstAcceptTask = server.AcceptAsync(cancellationToken).AsTask();
        await using var client = await NamedPipeControlClient.ConnectAsync(pipeName, cancellationToken);
        await using var serverConnection = await firstAcceptTask;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.AcceptAsync(cancellationToken).AsTask());

        Assert.Equal("This server instance already accepted a client.", error.Message);
        _ = serverConnection;
    }

    private static ControlEnvelope CreateHelloEnvelope(Guid messageId) =>
        new(
            new ProtocolVersion(1, 0),
            "hello",
            messageId,
            DateTimeOffset.Parse("2026-07-16T00:00:00.000Z"),
            new Hello
            {
                Implementation = new ImplementationIdentity
                {
                    Language = "csharp",
                    Name = "DearStory.Test",
                    Toolchain = "test-toolchain",
                    Version = "1.0.0"
                },
                RequiredCapabilities = [ "control.handshake.v1" ],
                Role = PeerRole.Host,
                SupportedCapabilities = [ "control.handshake.v1", "story.run" ]
            });
}
