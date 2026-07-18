using DearStory.Core;
using Xunit;

namespace DearStory.Sdk.Tests;

public sealed class StoryVisualMetadataTests
{
    [Fact]
    public void Reflection_registry_projects_canonical_visual_flag_from_story_attribute()
    {
        var registry = ReflectionStoryRegistry.Create(
            typeof(StoryVisualMetadataTests).Assembly,
            new ReflectionStoryRegistryOptions
            {
                AllowReflectionFallback = true,
            });

        var descriptor = Assert.Single(
            registry.Descriptors,
            item => item.Id.Value == "buttons/primarymanaged");

        Assert.True(descriptor.Visual.SupportsCapture);
        Assert.True(descriptor.Visual.IncludeInCanonicalCorpus);
    }

    public sealed class StoryArgs
    {
        [StoryArg("label")]
        public string Label { get; set; } = "Save";
    }

    public static class VisualStories
    {
        [Story("buttons/primarymanaged", typeof(StoryArgs), IncludeInCanonicalCorpus = true)]
        public static void Render(StoryContext context)
        {
        }
    }
}
