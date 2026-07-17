using DearStory.Core;
using DearStory.Docs.StaticHtml;
using Xunit;

namespace DearStory.Docs.Tests;

public sealed class StaticSiteBuilderTests
{
    [Fact]
    public async Task BuildAsync_writes_index_html_for_story_docs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspaceDirectory = Path.Combine(Path.GetTempPath(), "dearstory-docs-tests", Guid.NewGuid().ToString("N"));
        var docsDirectory = Path.Combine(workspaceDirectory, "docs");
        var outputDirectory = Path.Combine(workspaceDirectory, "site");
        Directory.CreateDirectory(docsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(docsDirectory, "buttons-primary.md"),
            """
            # Primary Button

            :::story id="buttons/primary"
            :::
            """,
            cancellationToken);

        var builder = new StaticSiteBuilder();
        await builder.BuildAsync(
            new BuildRequest(
                docsDirectory,
                outputDirectory,
                [StoryDescriptor.Create("buttons/primary", "Buttons/Primary")]),
            cancellationToken);

        Assert.True(File.Exists(Path.Combine(outputDirectory, "index.html")));
        Assert.Contains("buttons/primary", await File.ReadAllTextAsync(Path.Combine(outputDirectory, "index.html"), cancellationToken));
    }
}
