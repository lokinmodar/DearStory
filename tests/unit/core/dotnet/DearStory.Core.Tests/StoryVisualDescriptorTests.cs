using Xunit;

namespace DearStory.Core.Tests;

public sealed class StoryVisualDescriptorTests
{
    [Fact]
    public void Create_assigns_default_visual_metadata()
    {
        var descriptor = StoryDescriptor.Create("buttons/primary", "Buttons/Primary");

        Assert.True(descriptor.Visual.SupportsCapture);
        Assert.False(descriptor.Visual.IncludeInCanonicalCorpus);
    }
}
