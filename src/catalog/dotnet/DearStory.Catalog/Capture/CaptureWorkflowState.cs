using DearStory.Capture;
using System.Runtime.Versioning;

namespace DearStory.Catalog.Capture;

/// <summary>
/// Tracks pending and completed visual capture work for the active catalog session.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CaptureWorkflowState
{
    /// <summary>
    /// Gets the capture request that is currently pending.
    /// </summary>
    /// <value>The pending capture request, or <see langword="null" /> when no capture is in flight.</value>
    public CatalogCaptureCommand? PendingRequest { get; private set; }

    /// <summary>
    /// Gets the last completed capture result.
    /// </summary>
    /// <value>The last completed capture result, or <see langword="null" /> when no capture has completed yet.</value>
    public VisualCaptureResult? LastResult { get; private set; }

    /// <summary>
    /// Marks one capture request as pending.
    /// </summary>
    /// <param name="command">The capture request that is about to execute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="command" /> is <see langword="null" />.</exception>
    public void Begin(CatalogCaptureCommand command)
    {
        PendingRequest = command ?? throw new ArgumentNullException(nameof(command));
    }

    /// <summary>
    /// Marks one capture request as completed and stores its result.
    /// </summary>
    /// <param name="result">The completed capture result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public void Complete(VisualCaptureResult result)
    {
        LastResult = result ?? throw new ArgumentNullException(nameof(result));
        PendingRequest = null;
    }
}

/// <summary>
/// Describes one catalog-issued visual capture request.
/// </summary>
/// <param name="StoryId">The story identifier to capture.</param>
/// <param name="Backend">One of the enumeration values that specifies the backend to use.</param>
/// <param name="ApproveCanonical"><see langword="true" /> to request canonical approval; otherwise, <see langword="false" />.</param>
public sealed record CatalogCaptureCommand(string StoryId, CaptureBackendKind Backend, bool ApproveCanonical);
