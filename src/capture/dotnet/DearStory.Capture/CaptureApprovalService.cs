namespace DearStory.Capture;

/// <summary>
/// Enforces canonical approval guardrails for visual capture results.
/// </summary>
public static class CaptureApprovalService
{
    /// <summary>
    /// Validates that a visual capture result can be used for canonical approval.
    /// </summary>
    /// <param name="result">The capture result to validate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The result backend is not <see cref="CaptureBackendKind.Warp" />.</exception>
    public static void ValidateApproval(VisualCaptureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Backend != CaptureBackendKind.Warp)
        {
            throw new InvalidOperationException("Canonical approval requires a WARP capture result.");
        }
    }
}
