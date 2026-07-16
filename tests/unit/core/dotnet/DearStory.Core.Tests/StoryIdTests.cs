using Xunit;

namespace DearStory.Core.Tests;

public sealed class StoryIdTests
{
    [Theory]
    [InlineData("Buttons/Primary", "buttons/primary")]
    [InlineData(" buttons/Primary ", "buttons/primary")]
    [InlineData("Buttons\\Primary", "buttons/primary")]
    public void Parse_canonicalizes_story_ids(string raw, string expected)
        => Assert.Equal(expected, StoryId.Parse(raw).Value);
}
