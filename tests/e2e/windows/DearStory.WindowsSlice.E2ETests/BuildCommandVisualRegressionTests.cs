using Xunit;

namespace DearStory.WindowsSlice.E2ETests;

public sealed class BuildCommandVisualRegressionTests
{
    [Fact]
    public async Task Build_command_writes_real_capture_manifest_and_docs_screenshots()
    {
        using var environment = new VisualArtifactEnvironment();

        var result = await DearStoryCommand.RunAsync(
            "build",
            ".\\examples\\workspaces\\windows-slice",
            "--configuration", "Release",
            "--visual-backend", "warp");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("capture-results.json", result.OutputFiles);
        Assert.Contains("buttons-primary.png", result.OutputFiles);
    }

    [Fact]
    public async Task Build_command_rejects_gpu_approval_for_canonical_promotion()
    {
        using var environment = new VisualArtifactEnvironment();

        var result = await DearStoryCommand.RunAsync(
            "build",
            ".\\examples\\workspaces\\windows-slice",
            "--configuration", "Release",
            "--visual-backend", "gpu",
            "--approve");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("WARP", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }
}
