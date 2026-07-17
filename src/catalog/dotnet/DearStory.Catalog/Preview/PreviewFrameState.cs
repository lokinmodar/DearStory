using System.Runtime.Versioning;
using DearStory.Protocol.Generated;
using DearStory.Transport.Windows;

namespace DearStory.Catalog.Preview;

/// <summary>
/// Tracks the latest published preview frame for the active catalog session.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PreviewFrameState
{
    /// <summary>
    /// Gets the latest frame copied from shared memory.
    /// </summary>
    /// <value>The latest copied preview frame, or <see langword="null" /> when no frame has been read yet.</value>
    public FrameSlotLease? CurrentFrame { get; private set; }

    /// <summary>
    /// Gets the latest frame-presented message seen by the catalog.
    /// </summary>
    /// <value>The latest frame-presented message, or <see langword="null" /> when no frame has been observed yet.</value>
    public FramePresented? LastPresentedMessage { get; private set; }

    /// <summary>
    /// Updates the preview state from the latest shared-memory publication.
    /// </summary>
    /// <param name="message">The frame-presented message announcing the latest slot publication.</param>
    /// <param name="reader">The shared-memory frame reader bound to the active session.</param>
    public void Update(FramePresented message, SharedMemoryFrameReader reader)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(reader);

        LastPresentedMessage = message;
        if (reader.TryReadLatest(out var frame))
        {
            CurrentFrame = frame;
        }
    }
}
