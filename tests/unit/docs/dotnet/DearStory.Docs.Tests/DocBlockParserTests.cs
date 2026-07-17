using DearStory.Docs.Markdown;
using Xunit;

namespace DearStory.Docs.Tests;

public sealed class DocBlockParserTests
{
    [Fact]
    public void Parse_recognizes_story_controls_and_source_blocks()
    {
        var document = DocBlockParser.Parse(
            """
            # Primary Button
            :::story id="buttons/primary"
            :::
            :::controls
            :::
            :::source language="cpp"
            :::
            """);

        Assert.Equal(3, document.Blocks.Count(block => block.Kind is "story" or "controls" or "source"));
    }
}
