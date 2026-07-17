namespace DearStory.Capture;

/// <summary>
/// Represents one story-level visual capture outcome.
/// </summary>
/// <param name="StoryId">The canonical story identifier.</param>
/// <param name="HostId">The host identifier that produced the result.</param>
/// <param name="Backend">One of the enumeration values that specifies the backend used for capture.</param>
/// <param name="Classification">One of the enumeration values that specifies the comparison outcome.</param>
/// <param name="ActualImagePath">The path to the actual captured image.</param>
/// <param name="BaselineImagePath">The optional path to the baseline image.</param>
/// <param name="DiffImagePath">The optional path to the generated diff image.</param>
/// <param name="ManifestPath">The path to the capture manifest entry or file.</param>
public sealed record VisualCaptureResult(
    string StoryId,
    string HostId,
    CaptureBackendKind Backend,
    ComparisonClassification Classification,
    string ActualImagePath,
    string? BaselineImagePath,
    string? DiffImagePath,
    string ManifestPath);
