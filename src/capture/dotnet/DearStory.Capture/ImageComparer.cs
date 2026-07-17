using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace DearStory.Capture;

/// <summary>
/// Classifies visual capture results and writes deterministic diff images when required.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ImageComparer
{
    /// <summary>
    /// Classifies one captured image against its baseline image.
    /// </summary>
    /// <param name="actualPath">The path to the actual captured PNG image.</param>
    /// <param name="baselinePath">The path to the baseline PNG image.</param>
    /// <param name="backend">One of the enumeration values that specifies the backend used for capture.</param>
    /// <param name="approvingCanonical"><see langword="true" /> to validate a canonical approval operation; otherwise, <see langword="false" />.</param>
    /// <param name="diffPath">The optional path where a diff PNG should be written for mismatched images.</param>
    /// <returns>The comparison result.</returns>
    /// <exception cref="ArgumentException"><paramref name="actualPath" /> or <paramref name="baselinePath" /> is empty or whitespace.</exception>
    public static ComparisonResult Classify(
        string actualPath,
        string baselinePath,
        CaptureBackendKind backend,
        bool approvingCanonical,
        string? diffPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);

        if (approvingCanonical && backend != CaptureBackendKind.Warp)
        {
            return new ComparisonResult(ComparisonClassification.BackendMismatch, DiffImagePath: null);
        }

        if (!File.Exists(baselinePath))
        {
            return new ComparisonResult(ComparisonClassification.MissingBaseline, DiffImagePath: null);
        }

        using var actual = new Bitmap(actualPath);
        using var baseline = new Bitmap(baselinePath);

        var classification = AreImagesIdentical(actual, baseline)
            ? ComparisonClassification.Match
            : ComparisonClassification.Mismatch;

        if (classification == ComparisonClassification.Mismatch && !string.IsNullOrWhiteSpace(diffPath))
        {
            var resolvedDiffPath = Path.GetFullPath(diffPath);
            WriteDiff(actual, baseline, resolvedDiffPath);
            return new ComparisonResult(classification, resolvedDiffPath);
        }

        return new ComparisonResult(classification, DiffImagePath: null);
    }

    private static bool AreImagesIdentical(Bitmap actual, Bitmap baseline)
    {
        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            return false;
        }

        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                if (actual.GetPixel(x, y).ToArgb() != baseline.GetPixel(x, y).ToArgb())
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void WriteDiff(Bitmap actual, Bitmap baseline, string diffPath)
    {
        var parent = Path.GetDirectoryName(diffPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        using var diff = new Bitmap(Math.Max(actual.Width, baseline.Width), Math.Max(actual.Height, baseline.Height));
        for (var y = 0; y < diff.Height; y++)
        {
            for (var x = 0; x < diff.Width; x++)
            {
                var actualInBounds = x < actual.Width && y < actual.Height;
                var baselineInBounds = x < baseline.Width && y < baseline.Height;
                var actualColor = actualInBounds ? actual.GetPixel(x, y) : Color.Transparent;
                var baselineColor = baselineInBounds ? baseline.GetPixel(x, y) : Color.Transparent;

                diff.SetPixel(
                    x,
                    y,
                    actualInBounds && baselineInBounds && actualColor.ToArgb() == baselineColor.ToArgb()
                        ? actualColor
                        : Color.Magenta);
            }
        }

        diff.Save(diffPath, ImageFormat.Png);
    }
}

/// <summary>
/// Represents the outcome of comparing one actual image to one baseline image.
/// </summary>
/// <param name="Classification">One of the enumeration values that specifies the comparison outcome.</param>
/// <param name="DiffImagePath">The optional path to the generated diff image.</param>
public sealed record ComparisonResult(ComparisonClassification Classification, string? DiffImagePath);
