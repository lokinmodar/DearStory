using DearStory.Runner.Configuration;
using Xunit;

namespace DearStory.Runner.Tests;

public sealed class VisualWorkspaceConfigurationLoaderTests
{
    [Fact]
    public void LoadFromText_binds_visual_overrides_for_the_canonical_corpus()
    {
        var configuration = WorkspaceConfigurationLoader.LoadFromText(
            """
            [workspace]
            name = "windows-slice"

            [visual]
            [[visual.overrides]]
            story = "buttons/primary"
            include_in_canonical_corpus = true
            """);

        var entry = Assert.Single(configuration.Visual.Overrides);
        Assert.Equal("buttons/primary", entry.StoryId);
        Assert.True(entry.IncludeInCanonicalCorpus);
    }
}
