using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using DearStory.Catalog;
using DearStory.Catalog.Controls;
using DearStory.Catalog.Diagnostics;
using DearStory.Catalog.Preview;
using DearStory.Core;
using DearStory.Core.Schemas;
using DearStory.Protocol.Generated;
using DearStory.Transport.Windows;
using Xunit;

namespace DearStory.Catalog.Tests;

[SupportedOSPlatform("windows")]
public sealed class CatalogSessionPresenterTests
{
    [Fact]
    public void ApplyPatch_updates_current_arguments_when_the_patch_is_valid()
    {
        var presenter = new CatalogSessionPresenter(new StoryCatalog(), new PreviewFrameState(), new SchemaControlFactory());
        presenter.BindArguments(
            ArgumentSchema.Parse(
                """
                {
                  "type": "object",
                  "properties": {
                    "label": { "type": "string" }
                  },
                  "required": ["label"]
                }
                """),
            new JsonObject
            {
                ["label"] = "Save",
            });

        presenter.ApplyPatch("label", JsonValue.Create("Ship"));

        Assert.Equal("Ship", presenter.CurrentArguments["label"]!.GetValue<string>());
        Assert.Empty(presenter.Diagnostics);
    }

    [Fact]
    public void RecordHostResult_keeps_latest_host_diagnostics_available_to_the_catalog()
    {
        var presenter = new CatalogSessionPresenter(new StoryCatalog(), new PreviewFrameState(), new SchemaControlFactory());
        var diagnostic = new CatalogHostDiagnostic(
            DateTimeOffset.Parse("2026-07-17T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "runner.host.faulted",
            CatalogDiagnosticSeverity.Error,
            "Host faulted.");

        presenter.RecordHostResult(
            new HostHealthSnapshot(
                "cpp-host",
                CatalogHostState.Faulted,
                3,
                [diagnostic]));

        var host = Assert.Single(presenter.DiagnosticsPanel.Hosts);
        Assert.Equal("cpp-host", host.HostId);
        Assert.Equal(CatalogHostState.Faulted, host.State);
        Assert.Equal("runner.host.faulted", Assert.Single(host.Diagnostics).Code);
    }

    [Fact]
    public void UpdatePreview_reads_the_latest_shared_memory_frame()
    {
        var descriptor = FrameTransportDescriptor.Create($"Local\\dearstory-catalog-preview-{Guid.NewGuid():N}", 2, 2, 8, 3);
        using var writer = new SharedMemoryFrameWriter(descriptor);
        using var reader = new SharedMemoryFrameReader(descriptor);
        var preview = new PreviewFrameState();
        var published = writer.Publish(
            [
                255, 0, 0, 255,
                0, 255, 0, 255,
                0, 0, 255, 255,
                255, 255, 255, 255,
            ]);

        preview.Update(
            new FramePresented
            {
                Sequence = published.Sequence,
                SessionId = Guid.NewGuid(),
                SlotIndex = published.SlotIndex,
                TimestampUtc = "2026-07-17T00:00:00.000Z",
            },
            reader);

        Assert.NotNull(preview.CurrentFrame);
        Assert.Equal(published.Sequence, preview.CurrentFrame!.Sequence);
        Assert.Equal(16, preview.CurrentFrame.Bytes.Length);
    }
}
