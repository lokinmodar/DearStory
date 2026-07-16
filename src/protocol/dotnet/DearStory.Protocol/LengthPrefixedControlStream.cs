using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace DearStory.Protocol;

/// <summary>Provides exact-length framing for DearStory control messages.</summary>
public static class LengthPrefixedControlStream
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Gets the maximum supported control-frame payload size in bytes.</summary>
    public const int MaxFrameBytes = 1_048_576;

    /// <summary>Reads the next framed UTF-8 control payload from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>The decoded payload bytes, or <see langword="null" /> at clean EOF before a prefix.</returns>
    public static ValueTask<byte[]?> ReadAsync(Stream stream, CancellationToken cancellationToken) =>
        ReadAsync(stream, ArrayPool<byte>.Shared, cancellationToken);

    /// <summary>Reads the next framed UTF-8 control payload from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="arrayPool">The array pool used for temporary payload storage.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>The decoded payload bytes, or <see langword="null" /> at clean EOF before a prefix.</returns>
    public static async ValueTask<byte[]?> ReadAsync(Stream stream, ArrayPool<byte> arrayPool, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(arrayPool);

        var prefix = new byte[sizeof(uint)];
        var prefixBytesRead = await ReadAtLeastAsync(stream, prefix, allowCleanEof: true, cancellationToken).ConfigureAwait(false);
        if (prefixBytesRead == 0)
        {
            return null;
        }

        if (prefixBytesRead < prefix.Length)
        {
            throw new ProtocolException("protocol.invalid_envelope", "The control frame ended before the length prefix completed.");
        }

        var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (declaredSize > MaxFrameBytes)
        {
            throw new ProtocolException("protocol.frame_too_large", "The declared control frame exceeds the 1 MiB limit.");
        }

        if (declaredSize == 0)
        {
            return [];
        }

        var rented = arrayPool.Rent((int)declaredSize);
        try
        {
            var payloadBytesRead = await ReadAtLeastAsync(stream, rented.AsMemory(0, (int)declaredSize), allowCleanEof: false, cancellationToken).ConfigureAwait(false);
            if (payloadBytesRead < declaredSize)
            {
                throw new ProtocolException("protocol.invalid_envelope", "The control frame ended before the payload completed.");
            }

            _ = StrictUtf8.GetString(rented, 0, (int)declaredSize);
            var result = new byte[declaredSize];
            Buffer.BlockCopy(rented, 0, result, 0, (int)declaredSize);
            return result;
        }
        catch (DecoderFallbackException exception)
        {
            throw new ProtocolException("protocol.invalid_envelope", exception.Message);
        }
        finally
        {
            arrayPool.Return(rented, clearArray: true);
        }
    }

    /// <summary>Writes one length-prefixed UTF-8 control payload to a stream.</summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="payload">The UTF-8 payload bytes to write.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    public static async ValueTask WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (payload.Length > MaxFrameBytes)
        {
            throw new ProtocolException("protocol.frame_too_large", "The declared control frame exceeds the 1 MiB limit.");
        }

        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<int> ReadAtLeastAsync(
        Stream stream,
        Memory<byte> buffer,
        bool allowCleanEof,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (allowCleanEof)
                {
                    return totalRead;
                }

                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
