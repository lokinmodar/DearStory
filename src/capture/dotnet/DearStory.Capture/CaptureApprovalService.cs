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

        ValidateApproval(result.Backend);
    }

    /// <summary>
    /// Promotes one approved actual image into the canonical baseline location.
    /// </summary>
    /// <param name="actualImagePath">The path to the actual image that should become canonical.</param>
    /// <param name="baselineImagePath">The canonical baseline path to create or replace.</param>
    /// <param name="backend">One of the enumeration values that specifies the backend used for capture.</param>
    /// <exception cref="ArgumentException"><paramref name="actualImagePath" /> or <paramref name="baselineImagePath" /> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="actualImagePath" /> does not exist.</exception>
    /// <exception cref="InvalidOperationException">The result backend is not <see cref="CaptureBackendKind.Warp" />.</exception>
    public static void PromoteActualToBaseline(string actualImagePath, string baselineImagePath, CaptureBackendKind backend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineImagePath);

        ValidateApproval(backend);

        if (!File.Exists(actualImagePath))
        {
            throw new FileNotFoundException("The actual image to approve was not found.", actualImagePath);
        }

        var baselineDirectory = Path.GetDirectoryName(baselineImagePath);
        if (!string.IsNullOrEmpty(baselineDirectory))
        {
            Directory.CreateDirectory(baselineDirectory);
        }

        File.Copy(actualImagePath, baselineImagePath, overwrite: true);
    }

    private static void ValidateApproval(CaptureBackendKind backend)
    {
        if (backend != CaptureBackendKind.Warp)
        {
            throw new InvalidOperationException("Canonical approval requires a WARP capture result.");
        }
    }
}
