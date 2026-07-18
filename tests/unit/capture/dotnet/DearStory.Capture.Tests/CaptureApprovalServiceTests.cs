using DearStory.Capture;
using Xunit;

namespace DearStory.Capture.Tests;

public sealed class CaptureApprovalServiceTests
{
    [Fact]
    public void Approve_rejects_gpu_results_for_canonical_promotion()
    {
        var result = new VisualCaptureResult(
            StoryId: "buttons/primary",
            HostId: "cpp-host",
            Backend: CaptureBackendKind.Gpu,
            Classification: ComparisonClassification.Mismatch,
            ActualImagePath: "actual.png",
            BaselineImagePath: "baseline.png",
            DiffImagePath: "diff.png",
            ManifestPath: "capture-results.json");

        var error = Assert.Throws<InvalidOperationException>(() => CaptureApprovalService.ValidateApproval(result));
        Assert.Contains("WARP", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
