using DearStory.Core;
using System.Runtime.Versioning;
using Xunit;

namespace DearStory.Capture.Tests;

public sealed class CapturePolicyTests
{
    [Fact]
    public void ResolveCanonicalCorpus_combines_story_metadata_and_workspace_overrides()
    {
        StoryDescriptor[] stories =
        [
            StoryDescriptor.Create("buttons/primary", "Buttons/Primary") with
            {
                Visual = new StoryVisualDescriptor
                {
                    IncludeInCanonicalCorpus = false,
                },
            },
            StoryDescriptor.Create("buttons/primarymanaged", "Buttons/PrimaryManaged") with
            {
                Visual = new StoryVisualDescriptor
                {
                    IncludeInCanonicalCorpus = true,
                },
            },
        ];

        var overrides = new[]
        {
            new KeyValuePair<string, bool>("buttons/primary", true),
        };

        var resolved = CaptureCorpusResolver.ResolveCanonicalStories(stories, overrides);

        Assert.Collection(
            resolved.OrderBy(static item => item.Id.Value, StringComparer.Ordinal),
            item => Assert.Equal("buttons/primary", item.Id.Value),
            item => Assert.Equal("buttons/primarymanaged", item.Id.Value));
    }

    [Fact]
    public void ResolveArtifactLayout_places_baseline_in_repo_and_outputs_under_override_root()
    {
        var repoRoot = ResolveRepositoryRootFromTestLocation();
        var workspaceRoot = Path.Combine(repoRoot, "examples", "workspaces", "windows-slice");
        var overrideRoot = Path.Combine(Path.GetTempPath(), "DearStory.Capture.Tests", Guid.NewGuid().ToString("N"));

        var result = CaptureArtifactLayout.Resolve(
            workspaceRoot,
            "buttons/primary",
            "cpp-host",
            overrideRoot);

        Assert.EndsWith(
            Path.Combine("tests", "visual", "windows", "baselines", "buttons", "primary.png"),
            result.BaselineImagePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(overrideRoot, result.ActualImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(overrideRoot, result.DiffImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(overrideRoot, result.ManifestPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Path.Combine("tests", "visual", "windows", "baselines"),
            result.ActualImagePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Path.Combine("tests", "visual", "windows", "baselines"),
            result.DiffImagePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Path.Combine("tests", "visual", "windows", "baselines"),
            result.ManifestPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRepositoryRootFromTestLocation()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The DearStory repository root could not be resolved from the running test location.");
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ExecuteAsync_writes_actual_png_and_manifest_without_promoting_missing_baselines()
    {
        var root = Path.Combine(Path.GetTempPath(), "DearStory.Capture.Tests", Guid.NewGuid().ToString("N"));
        var repoRoot = Path.Combine(root, "repo");
        var workspaceRoot = Path.Combine(repoRoot, "examples", "workspaces", "windows-slice");
        var artifactRoot = Path.Combine(root, "artifacts");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(artifactRoot);
        File.WriteAllText(Path.Combine(repoRoot, "DearStory.slnx"), "<Solution />");

        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var service = new VisualCaptureService();
            var request = new VisualCaptureRequest(
                WorkspaceRoot: workspaceRoot,
                StoryIds: ["buttons/primary"],
                Backend: CaptureBackendKind.Warp,
                CanonicalOnly: false,
                ApproveCanonical: false,
                ArtifactRootOverride: artifactRoot);

            var results = await service.ExecuteAsync(
                request,
                new FakeVisualFrameSource(),
                cancellationToken);

            var result = Assert.Single(results);
            Assert.Equal(ComparisonClassification.MissingBaseline, result.Classification);
            Assert.True(File.Exists(result.ActualImagePath));
            Assert.True(File.Exists(result.ManifestPath));
            Assert.False(File.Exists(result.BaselineImagePath));
            Assert.Contains("buttons/primary", await File.ReadAllTextAsync(result.ManifestPath, cancellationToken));
            Assert.Contains("missing-baseline", await File.ReadAllTextAsync(result.ManifestPath, cancellationToken), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ExecuteAsync_approves_warp_capture_by_promoting_the_canonical_baseline()
    {
        var root = Path.Combine(Path.GetTempPath(), "DearStory.Capture.Tests", Guid.NewGuid().ToString("N"));
        var repoRoot = Path.Combine(root, "repo");
        var workspaceRoot = Path.Combine(repoRoot, "examples", "workspaces", "windows-slice");
        var artifactRoot = Path.Combine(root, "artifacts");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(artifactRoot);
        File.WriteAllText(Path.Combine(repoRoot, "DearStory.slnx"), "<Solution />");

        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var service = new VisualCaptureService();
            var request = new VisualCaptureRequest(
                WorkspaceRoot: workspaceRoot,
                StoryIds: ["buttons/primary"],
                Backend: CaptureBackendKind.Warp,
                CanonicalOnly: false,
                ApproveCanonical: true,
                ArtifactRootOverride: artifactRoot);

            var results = await service.ExecuteAsync(
                request,
                new FakeVisualFrameSource(),
                cancellationToken);

            var result = Assert.Single(results);
            Assert.Equal(ComparisonClassification.Match, result.Classification);
            Assert.True(File.Exists(result.ActualImagePath));
            Assert.True(File.Exists(result.BaselineImagePath));
            Assert.Equal(
                File.ReadAllBytes(result.ActualImagePath),
                File.ReadAllBytes(result.BaselineImagePath!));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeVisualFrameSource : IVisualFrameSource
    {
        public Task<CapturedFrame> CaptureAsync(string storyId, CaptureBackendKind backend, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new CapturedFrame(
                    StoryId: storyId,
                    HostId: "cpp-host",
                    Width: 1,
                    Height: 1,
                    Stride: 4,
                    RgbaBytes: new byte[] { 255, 0, 0, 255 },
                    TimestampUtc: DateTimeOffset.Parse("2026-07-17T12:00:00Z")));
        }
    }
}
