using Xunit;

namespace DearStory.WindowsSlice.E2ETests;

public sealed class DevCommandCaptureTests
{
    [Fact]
    public async Task Dev_command_capture_story_writes_visual_results_without_entering_the_full_interactive_loop()
    {
        using var environment = new VisualArtifactEnvironment();

        var result = await DearStoryCommand.RunAsync(
            "dev",
            ".\\examples\\workspaces\\windows-slice",
            "--capture-story", "buttons/primary",
            "--visual-backend", "warp");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("capture-results.json", result.StandardOutput + result.StandardError, StringComparison.OrdinalIgnoreCase);
    }
}
