namespace DearStory.Transport.Windows;

/// <summary>Represents the immutable geometry and slot layout for a Windows shared-memory RGBA frame channel.</summary>
/// <remarks>
/// The descriptor is shared by writers and readers so both sides agree on the mapping name,
/// RGBA8 frame size, and slot count without introducing a transport-specific protocol type.
/// </remarks>
public sealed class FrameTransportDescriptor
{
    /// <summary>Identifies the byte offset of the published frame sequence within a slot.</summary>
    internal const int SequenceOffset = 0;

    /// <summary>Identifies the byte offset of the RGBA payload length within a slot.</summary>
    internal const int PayloadLengthOffset = 8;

    /// <summary>Identifies the byte offset where RGBA payload bytes begin within a slot.</summary>
    internal const int PayloadOffset = 16;

    private FrameTransportDescriptor(string mappingName, int width, int height, int stride, int slotCount)
    {
        MappingName = mappingName;
        Width = width;
        Height = height;
        Stride = stride;
        SlotCount = slotCount;
    }

    /// <summary>Gets the Windows memory-mapping name.</summary>
    /// <value>The opaque mapping name used with <c>CreateFileMappingW</c> and <c>MemoryMappedFile</c>.</value>
    public string MappingName { get; }

    /// <summary>Gets the frame width in pixels.</summary>
    /// <value>The horizontal pixel count for each published frame.</value>
    public int Width { get; }

    /// <summary>Gets the frame height in pixels.</summary>
    /// <value>The vertical pixel count for each published frame.</value>
    public int Height { get; }

    /// <summary>Gets the row stride in bytes.</summary>
    /// <value>The row stride, in bytes, for RGBA8 pixels.</value>
    public int Stride { get; }

    /// <summary>Gets the number of slots in the shared-memory ring.</summary>
    /// <value>The number of reusable frame slots available to the writer.</value>
    public int SlotCount { get; }

    /// <summary>Gets the byte length of one RGBA frame.</summary>
    /// <value>The payload byte count for a single RGBA8 frame.</value>
    public int FrameByteLength => checked(Height * Stride);

    /// <summary>Gets the byte length of one slot including metadata.</summary>
    /// <value>The slot byte count including sequence and payload-length metadata.</value>
    public int SlotByteLength => checked(PayloadOffset + FrameByteLength);

    /// <summary>Gets the total byte length of the mapping.</summary>
    /// <value>The total byte count reserved for the entire shared-memory mapping.</value>
    public long TotalByteLength => checked((long)SlotCount * SlotByteLength);

    /// <summary>Creates a validated frame transport descriptor.</summary>
    /// <param name="mappingName">The opaque Windows memory-mapping name.</param>
    /// <param name="width">The horizontal pixel count for each frame.</param>
    /// <param name="height">The vertical pixel count for each frame.</param>
    /// <param name="stride">The row stride, in bytes, for the RGBA8 frame payload.</param>
    /// <param name="slotCount">The number of reusable frame slots to reserve in shared memory.</param>
    /// <returns>A validated descriptor that both ends of the channel can reuse.</returns>
    /// <exception cref="ArgumentException"><paramref name="mappingName" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width" />, <paramref name="height" />, or <paramref name="slotCount" /> is not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stride" /> is smaller than the RGBA8 payload width.</exception>
    public static FrameTransportDescriptor Create(string mappingName, int width, int height, int stride, int slotCount)
    {
        if (string.IsNullOrWhiteSpace(mappingName))
        {
            throw new ArgumentException("The mapping name must be provided.", nameof(mappingName));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The frame width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "The frame height must be positive.");
        }

        if (stride < width * 4)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), "The frame stride must be at least width * 4 bytes for RGBA8 pixels.");
        }

        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "The slot count must be positive.");
        }

        return new FrameTransportDescriptor(mappingName, width, height, stride, slotCount);
    }

    /// <summary>Computes the byte offset of a frame slot within the mapping.</summary>
    /// <param name="slotIndex">The zero-based slot index.</param>
    /// <returns>The byte offset of the requested slot.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotIndex" /> is outside the configured slot range.</exception>
    internal long GetSlotOffset(int slotIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        if (slotIndex >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "The slot index must be within the configured slot count.");
        }

        return checked((long)slotIndex * SlotByteLength);
    }
}
