namespace DearStory.Transport.Windows;

/// <summary>Represents one published frame snapshot read from shared memory.</summary>
public sealed class FrameSlotLease
{
    /// <summary>Initializes a new instance of the <see cref="FrameSlotLease" /> class.</summary>
    /// <param name="slotIndex">The zero-based slot index that supplied the frame bytes.</param>
    /// <param name="sequence">The monotonic frame sequence assigned by the writer.</param>
    /// <param name="bytes">The copied RGBA payload bytes for the published frame.</param>
    public FrameSlotLease(int slotIndex, long sequence, ReadOnlyMemory<byte> bytes)
    {
        SlotIndex = slotIndex;
        Sequence = sequence;
        Bytes = bytes;
    }

    /// <summary>Gets the source slot index.</summary>
    /// <value>The zero-based slot index that contained the published frame.</value>
    public int SlotIndex { get; }

    /// <summary>Gets the monotonic frame sequence.</summary>
    /// <value>The writer-assigned sequence for the published frame.</value>
    public long Sequence { get; }

    /// <summary>Gets the copied RGBA payload bytes.</summary>
    /// <value>The copied RGBA8 payload for the published frame.</value>
    public ReadOnlyMemory<byte> Bytes { get; }
}
