using DearStory.Capture;
using DearStory.Catalog;
using DearStory.Catalog.Capture;
using DearStory.Catalog.Controls;
using DearStory.Catalog.Preview;
using DearStory.Core;
using System.Runtime.Versioning;
using Xunit;

namespace DearStory.Catalog.Tests;

[SupportedOSPlatform("windows")]
public sealed class CatalogCaptureWorkflowTests
{
    [Fact]
    public void RequestCapture_tracks_pending_command_and_last_result()
    {
        var presenter = new CatalogSessionPresenter(new StoryCatalog(), new PreviewFrameState(), new SchemaControlFactory());
        var command = new CatalogCaptureCommand("buttons/primary", CaptureBackendKind.Warp, ApproveCanonical: false);

        presenter.RequestCapture(command);
        Assert.Equal(command, presenter.CaptureWorkflow.PendingRequest);

        presenter.CompleteCapture(
            new VisualCaptureResult(
                StoryId: "buttons/primary",
                HostId: "cpp-host",
                Backend: CaptureBackendKind.Warp,
                Classification: ComparisonClassification.Match,
                ActualImagePath: "actual.png",
                BaselineImagePath: "baseline.png",
                DiffImagePath: null,
                ManifestPath: "capture-results.json"));

        Assert.Null(presenter.CaptureWorkflow.PendingRequest);
        Assert.NotNull(presenter.CaptureWorkflow.LastResult);
    }
}
