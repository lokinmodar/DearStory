using Xunit;

namespace DearStory.WindowsSlice.Tests;

public sealed class HotReloadIntegrationTests
{
    [Fact]
    public async Task Dev_loop_restarts_only_affected_host_and_preserves_arguments()
    {
        await using var harness = await WindowsSliceHarness.StartAsync();

        await harness.SelectStoryAsync("buttons/primary");
        await harness.ApplyArgumentAsync("label", "Ship");
        await harness.TouchCppStoryAsync();

        var restart = await harness.WaitForHostRestartAsync("cpp-host");

        Assert.Equal("cpp-host", restart.HostId);
        Assert.Equal("Ship", await harness.ReadCurrentArgumentAsync("label"));
        Assert.False(await harness.WasHostRestartedAsync("dotnet-host"));
    }
}
