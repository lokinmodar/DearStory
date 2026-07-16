using System.Buffers;
using System.Text;
using Xunit;

namespace DearStory.Protocol.Tests;

public sealed class LengthPrefixedControlStreamTests
{
    [Fact]
    public async Task ReadAsync_decodes_a_frame_split_one_byte_at_a_time()
    {
        var payload = Encoding.UTF8.GetBytes("""{"type":"hello"}""");
        await using var stream = new ChunkedReadStream(Prefix(payload.Length).Concat(payload).ToArray(), 1);

        var decoded = await LengthPrefixedControlStream.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(payload, decoded);
    }

    [Fact]
    public async Task ReadAsync_decodes_two_frames_in_order()
    {
        var first = Encoding.UTF8.GetBytes("""{"type":"hello"}""");
        var second = Encoding.UTF8.GetBytes("""{"type":"welcome"}""");
        await using var stream = new MemoryStream();

        await LengthPrefixedControlStream.WriteAsync(stream, first, CancellationToken.None);
        await LengthPrefixedControlStream.WriteAsync(stream, second, CancellationToken.None);
        stream.Position = 0;

        var decodedFirst = await LengthPrefixedControlStream.ReadAsync(stream, CancellationToken.None);
        var decodedSecond = await LengthPrefixedControlStream.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(first, decodedFirst);
        Assert.Equal(second, decodedSecond);
    }

    [Fact]
    public async Task ReadAsync_accepts_a_zero_length_frame()
    {
        await using var stream = new MemoryStream(Prefix(0));

        var decoded = await LengthPrefixedControlStream.ReadAsync(stream, CancellationToken.None);

        Assert.NotNull(decoded);
        Assert.Empty(decoded);
    }

    [Fact]
    public async Task ReadAsync_rejects_invalid_utf8()
    {
        var invalid = Prefix(2).Concat([ (byte)0xC3, (byte)0x28 ]).ToArray();
        await using var stream = new MemoryStream(invalid);

        var error = await Assert.ThrowsAsync<ProtocolException>(
            () => LengthPrefixedControlStream.ReadAsync(stream, CancellationToken.None).AsTask());

        Assert.Equal("protocol.invalid_envelope", error.Code);
    }

    [Fact]
    public async Task ReadAsync_rejects_size_before_renting_payload_buffer()
    {
        var prefix = BitConverter.GetBytes(LengthPrefixedControlStream.MaxFrameBytes + 1);
        await using var input = new MemoryStream(prefix);
        var pool = new RecordingArrayPool<byte>();

        var error = await Assert.ThrowsAsync<ProtocolException>(
            () => LengthPrefixedControlStream.ReadAsync(input, pool, CancellationToken.None).AsTask());

        Assert.Equal("protocol.frame_too_large", error.Code);
        Assert.Empty(pool.RequestedLengths);
    }

    [Fact]
    public async Task ReadAsync_returns_null_on_clean_eof_before_prefix()
    {
        await using var stream = new MemoryStream();

        var decoded = await LengthPrefixedControlStream.ReadAsync(stream, CancellationToken.None);

        Assert.Null(decoded);
    }

    private static byte[] Prefix(int payloadLength) => BitConverter.GetBytes(payloadLength);

    private sealed class ChunkedReadStream(byte[] bytes, int chunkSize) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, writable: false);

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, Math.Min(count, chunkSize));

        public override int Read(Span<byte> buffer) =>
            _inner.Read(buffer[..Math.Min(buffer.Length, chunkSize)]);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
