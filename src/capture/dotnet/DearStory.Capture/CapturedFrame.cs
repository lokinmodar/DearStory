namespace DearStory.Capture;

/// <summary>
/// Represents one captured RGBA frame from a DearStory host.
/// </summary>
/// <param name="StoryId">The canonical story identifier.</param>
/// <param name="HostId">The host identifier that produced the frame.</param>
/// <param name="Width">The frame width in pixels.</param>
/// <param name="Height">The frame height in pixels.</param>
/// <param name="Stride">The frame stride in bytes.</param>
/// <param name="RgbaBytes">The packed RGBA byte payload.</param>
/// <param name="TimestampUtc">The UTC timestamp when the frame was captured.</param>
public sealed record CapturedFrame(
    string StoryId,
    string HostId,
    int Width,
    int Height,
    int Stride,
    ReadOnlyMemory<byte> RgbaBytes,
    DateTimeOffset TimestampUtc);
