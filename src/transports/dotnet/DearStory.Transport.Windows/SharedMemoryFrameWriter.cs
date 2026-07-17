using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Threading;

namespace DearStory.Transport.Windows;

/// <summary>Publishes RGBA frames into one Windows shared-memory mapping.</summary>
/// <remarks>
/// The writer reserves the first 16 bytes of each slot for metadata and publishes the frame
/// sequence only after the payload bytes have been flushed so readers can ignore incomplete writes.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class SharedMemoryFrameWriter : IDisposable
{
    private readonly FrameTransportDescriptor _descriptor;
    private readonly MemoryMappedFile _mapping;
    private long _sequence;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SharedMemoryFrameWriter" /> class.</summary>
    /// <param name="descriptor">The descriptor that defines the mapping name and RGBA slot layout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor" /> is <see langword="null" />.</exception>
    public SharedMemoryFrameWriter(FrameTransportDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptor = descriptor;
        _mapping = MemoryMappedFile.CreateOrOpen(
            descriptor.MappingName,
            descriptor.TotalByteLength,
            MemoryMappedFileAccess.ReadWrite);
    }

    /// <summary>Publishes a complete RGBA frame into the next slot.</summary>
    /// <param name="rgbaBytes">The RGBA8 payload bytes whose length must equal the configured frame byte length.</param>
    /// <returns>A snapshot describing the written slot, published sequence, and copied payload bytes.</returns>
    /// <exception cref="ObjectDisposedException">The writer has already been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="rgbaBytes" /> does not match the configured frame byte length.</exception>
    public FrameSlotLease Publish(ReadOnlySpan<byte> rgbaBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (rgbaBytes.Length != _descriptor.FrameByteLength)
        {
            throw new ArgumentException(
                $"The RGBA payload must contain exactly {_descriptor.FrameByteLength} bytes.",
                nameof(rgbaBytes));
        }

        var sequence = Interlocked.Increment(ref _sequence);
        var slotIndex = (int)((sequence - 1) % _descriptor.SlotCount);
        var payload = rgbaBytes.ToArray();

        using var accessor = _mapping.CreateViewAccessor(
            _descriptor.GetSlotOffset(slotIndex),
            _descriptor.SlotByteLength,
            MemoryMappedFileAccess.ReadWrite);

        accessor.Write(FrameTransportDescriptor.SequenceOffset, 0L);
        accessor.Write(FrameTransportDescriptor.PayloadLengthOffset, payload.Length);
        accessor.WriteArray(FrameTransportDescriptor.PayloadOffset, payload, 0, payload.Length);
        accessor.Flush();
        accessor.Write(FrameTransportDescriptor.SequenceOffset, sequence);
        accessor.Flush();

        return new FrameSlotLease(slotIndex, sequence, payload);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mapping.Dispose();
        _disposed = true;
    }
}
