using DearStory.Capture;
using DearStory.Runner.Capture;
using DearStory.Runner.Configuration;
using Xunit;

namespace DearStory.WindowsSlice.Tests;

public sealed class RealCaptureIntegrationTests
{
    [Theory]
    [InlineData("cpp-host", "buttons/primary")]
    [InlineData("dotnet-host", "buttons/primarymanaged")]
    public async Task Runner_capture_adapter_reads_real_rgba_frames_from_both_official_hosts(string hostId, string storyId)
    {
        var configuration = WorkspaceConfigurationLoader.Load(".\\examples\\workspaces\\windows-slice");
        var adapter = new RunnerHostCaptureAdapter(configuration);

        var frame = await adapter.CaptureAsync(storyId, CaptureBackendKind.Warp, TestContext.Current.CancellationToken);

        Assert.Equal(hostId, frame.HostId);
        Assert.True(frame.Width > 1);
        Assert.True(frame.Height > 1);
        Assert.True(frame.RgbaBytes.Length >= frame.Height * frame.Stride);
    }
}
