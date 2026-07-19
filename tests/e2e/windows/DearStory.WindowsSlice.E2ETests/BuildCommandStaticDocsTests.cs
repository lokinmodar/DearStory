using DearStory.Testing;
using Xunit;

namespace DearStory.WindowsSlice.E2ETests;

public sealed class BuildCommandStaticDocsTests
{
    [Fact]
    public async Task Build_command_emits_searchable_html_and_screenshot()
    {
        var result = await DearStoryCommand.RunAsync(
            "build",
            ".\\examples\\workspaces\\windows-slice",
            "--configuration",
            CurrentBuildConfiguration.CurrentConfiguration());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("index.html", result.OutputFiles);
        Assert.Contains("buttons-primary.png", result.OutputFiles);
    }
}
