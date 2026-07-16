using System.IO.Pipes;

namespace DearStory.Protocol.Windows;

/// <summary>Represents one framed DearStory control connection over a named pipe.</summary>
public sealed class NamedPipeConnection : IAsyncDisposable
{
    private readonly PipeStream _stream;

    internal NamedPipeConnection(PipeStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    /// <summary>Reads the next framed UTF-8 control payload from the pipe.</summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>The next framed payload, or <see langword="null" /> when the peer closes cleanly.</returns>
    public ValueTask<byte[]?> ReadAsync(CancellationToken cancellationToken) =>
        LengthPrefixedControlStream.ReadAsync(_stream, cancellationToken);

    /// <summary>Writes one framed UTF-8 control payload to the pipe.</summary>
    /// <param name="payload">The UTF-8 payload bytes to send.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        LengthPrefixedControlStream.WriteAsync(_stream, payload, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _stream.DisposeAsync();
}

/// <summary>Opens client-side DearStory control connections over Windows named pipes.</summary>
public static class NamedPipeControlClient
{
    /// <summary>Connects to one named-pipe control server.</summary>
    /// <param name="pipeName">The pipe name without the <c>\\.\pipe\</c> prefix.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A connected named-pipe control session.</returns>
    public static async ValueTask<NamedPipeConnection> ConnectAsync(string pipeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var stream = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
            stream.ReadMode = PipeTransmissionMode.Byte;
            return new NamedPipeConnection(stream);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
