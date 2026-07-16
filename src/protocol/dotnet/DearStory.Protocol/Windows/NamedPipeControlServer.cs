using System.IO.Pipes;

namespace DearStory.Protocol.Windows;

/// <summary>Accepts one client-side DearStory control connection over a Windows named pipe.</summary>
public sealed class NamedPipeControlServer : IAsyncDisposable
{
    private readonly NamedPipeServerStream _stream;
    private bool _accepted;
    private bool _transferred;

    /// <summary>Initializes a new instance of the <see cref="NamedPipeControlServer" /> class.</summary>
    /// <param name="pipeName">The pipe name without the <c>\\.\pipe\</c> prefix.</param>
    public NamedPipeControlServer(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        _stream = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    /// <summary>Accepts exactly one client for this server instance.</summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A connected named-pipe control session.</returns>
    /// <exception cref="InvalidOperationException">A client was already accepted by this server instance.</exception>
    public async ValueTask<NamedPipeConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        if (_accepted)
        {
            throw new InvalidOperationException("This server instance already accepted a client.");
        }

        await _stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        _accepted = true;
        _transferred = true;
        return new NamedPipeConnection(_stream);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() =>
        _transferred ? ValueTask.CompletedTask : _stream.DisposeAsync();
}
