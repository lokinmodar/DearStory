namespace DearStory.Runner.Capture;

/// <summary>Identifies one host/story pair for visual capture.</summary>
/// <param name="HostId">The stable host identifier that owns the story.</param>
/// <param name="StoryId">The canonical story identifier to capture.</param>
public sealed record StoryCaptureTarget(string HostId, string StoryId);
