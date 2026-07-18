namespace DearStory.Capture;

/// <summary>
/// Represents one shared visual capture execution request.
/// </summary>
/// <param name="WorkspaceRoot">The workspace root path for the capture operation.</param>
/// <param name="StoryIds">The story identifiers selected for capture.</param>
/// <param name="Backend">One of the enumeration values that specifies the capture backend.</param>
/// <param name="CanonicalOnly"><see langword="true" /> to restrict capture to canonical stories; otherwise, <see langword="false" />.</param>
/// <param name="ApproveCanonical"><see langword="true" /> to approve canonical outputs; otherwise, <see langword="false" />.</param>
/// <param name="ArtifactRootOverride">The optional artifact root override path.</param>
public sealed record VisualCaptureRequest(
    string WorkspaceRoot,
    IReadOnlyList<string> StoryIds,
    CaptureBackendKind Backend,
    bool CanonicalOnly,
    bool ApproveCanonical,
    string? ArtifactRootOverride);
