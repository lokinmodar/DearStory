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
}
