using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace DearStory.Transport.Windows;

/// <summary>Reads the latest RGBA frame snapshot from one Windows shared-memory mapping.</summary>
/// <remarks>
/// The reader scans every configured slot and returns the payload with the highest published sequence.
/// Slots whose sequence is still zero or whose payload length is invalid are ignored.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class SharedMemoryFrameReader : IDisposable
{
    private readonly FrameTransportDescriptor _descriptor;
    private readonly MemoryMappedFile _mapping;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SharedMemoryFrameReader" /> class.</summary>
    /// <param name="descriptor">The descriptor that defines the mapping name and RGBA slot layout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor" /> is <see langword="null" />.</exception>
    public SharedMemoryFrameReader(FrameTransportDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptor = descriptor;
        _mapping = MemoryMappedFile.CreateOrOpen(
            descriptor.MappingName,
            descriptor.TotalByteLength,
            MemoryMappedFileAccess.ReadWrite);
    }

    /// <summary>Attempts to read the latest published RGBA frame.</summary>
    /// <param name="frame">When this method returns, contains the copied frame snapshot when one is available. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true" /> when a frame was read; otherwise <see langword="false" />.</returns>
    /// <exception cref="ObjectDisposedException">The reader has already been disposed.</exception>
    public bool TryReadLatest(out FrameSlotLease frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        FrameSlotLease? latestFrame = null;
        long latestSequence = 0;

        for (var slotIndex = 0; slotIndex < _descriptor.SlotCount; slotIndex++)
        {
            using var accessor = _mapping.CreateViewAccessor(
                _descriptor.GetSlotOffset(slotIndex),
                _descriptor.SlotByteLength,
                MemoryMappedFileAccess.ReadWrite);

            var sequence = accessor.ReadInt64(FrameTransportDescriptor.SequenceOffset);
            if (sequence <= latestSequence)
            {
                continue;
            }

            var payloadLength = accessor.ReadInt32(FrameTransportDescriptor.PayloadLengthOffset);
            if (payloadLength <= 0 || payloadLength > _descriptor.FrameByteLength)
            {
                continue;
            }

            var payload = new byte[payloadLength];
            accessor.ReadArray(FrameTransportDescriptor.PayloadOffset, payload, 0, payload.Length);
            latestSequence = sequence;
            latestFrame = new FrameSlotLease(slotIndex, sequence, payload);
        }

        if (latestFrame is null)
        {
            frame = null!;
            return false;
        }

        frame = latestFrame;
        return true;
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
