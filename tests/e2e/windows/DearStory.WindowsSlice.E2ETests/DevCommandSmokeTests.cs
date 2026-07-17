using Xunit;

namespace DearStory.WindowsSlice.E2ETests;

public sealed class DevCommandSmokeTests
{
    [Fact]
    public async Task Dev_command_returns_success_for_the_windows_slice_workspace()
    {
        var result = await DearStoryCommand.RunAsync("dev", ".\\examples\\workspaces\\windows-slice");

        Assert.Equal(0, result.ExitCode);
    }
}
