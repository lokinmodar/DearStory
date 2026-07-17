using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Xunit;

namespace DearStory.Capture.Tests;

[SupportedOSPlatform("windows")]
public sealed class ImageComparerTests
{
    [Fact]
    public void Classify_marks_missing_baseline_when_baseline_file_is_absent()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var actualPath = WritePng(directory, "actual.png", 1, 1, [Color.Red]);
            var baselinePath = Path.Combine(directory, "missing.png");

            var result = ImageComparer.Classify(
                actualPath,
                baselinePath,
                CaptureBackendKind.Warp,
                approvingCanonical: false);

            Assert.Equal(ComparisonClassification.MissingBaseline, result.Classification);
            Assert.Null(result.DiffImagePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Classify_marks_backend_mismatch_when_approving_non_warp_results()
    {
        var result = ImageComparer.Classify(
            "actual.png",
            "baseline.png",
            CaptureBackendKind.Gpu,
            approvingCanonical: true);

        Assert.Equal(ComparisonClassification.BackendMismatch, result.Classification);
        Assert.Null(result.DiffImagePath);
    }

    [Fact]
    public void Classify_marks_identical_pngs_as_match()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var actualPath = WritePng(directory, "actual.png", 1, 1, [Color.Red]);
            var baselinePath = WritePng(directory, "baseline.png", 1, 1, [Color.Red]);

            var result = ImageComparer.Classify(
                actualPath,
                baselinePath,
                CaptureBackendKind.Warp,
                approvingCanonical: false);

            Assert.Equal(ComparisonClassification.Match, result.Classification);
            Assert.Null(result.DiffImagePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Classify_writes_deterministic_diff_png_for_mismatched_images()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var actualPath = WritePng(directory, "actual.png", 1, 1, [Color.Blue]);
            var baselinePath = WritePng(directory, "baseline.png", 1, 1, [Color.Red]);
            var diffPathA = Path.Combine(directory, "diff-a.png");
            var diffPathB = Path.Combine(directory, "diff-b.png");

            var firstResult = ImageComparer.Classify(
                actualPath,
                baselinePath,
                CaptureBackendKind.Warp,
                approvingCanonical: false,
                diffPathA);

            var secondResult = ImageComparer.Classify(
                actualPath,
                baselinePath,
                CaptureBackendKind.Warp,
                approvingCanonical: false,
                diffPathB);

            Assert.Equal(ComparisonClassification.Mismatch, firstResult.Classification);
            Assert.Equal(diffPathA, firstResult.DiffImagePath);
            Assert.Equal(ComparisonClassification.Mismatch, secondResult.Classification);
            Assert.Equal(diffPathB, secondResult.DiffImagePath);
            Assert.True(File.Exists(diffPathA));
            Assert.True(File.Exists(diffPathB));
            Assert.Equal(File.ReadAllBytes(diffPathA), File.ReadAllBytes(diffPathB));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "DearStory.Capture.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string WritePng(string directory, string fileName, int width, int height, IReadOnlyList<Color> pixels)
    {
        var path = Path.Combine(directory, fileName);

        using var bitmap = new Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, pixels[(y * width) + x]);
            }
        }

        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
