using DearStory.Core;
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
}
