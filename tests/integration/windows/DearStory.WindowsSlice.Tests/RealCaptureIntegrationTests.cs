using DearStory.Capture;
using DearStory.Runner.Capture;
using DearStory.Runner.Configuration;
using System.Runtime.Versioning;
using Xunit;

namespace DearStory.WindowsSlice.Tests;

[SupportedOSPlatform("windows")]
public sealed class RealCaptureIntegrationTests
{
    [Theory]
    [InlineData("cpp-host", "buttons/primary")]
    [InlineData("dotnet-host", "buttons/primarymanaged")]
    public async Task Runner_capture_adapter_reads_real_rgba_frames_from_both_official_hosts(string hostId, string storyId)
    {
        var configuration = WorkspaceConfigurationLoader.Load(ResolveWindowsSliceWorkspacePath());
        var adapter = new RunnerHostCaptureAdapter(configuration);

        var frame = await adapter.CaptureAsync(storyId, CaptureBackendKind.Warp, TestContext.Current.CancellationToken);

        Assert.Equal(hostId, frame.HostId);
        Assert.True(frame.Width > 1);
        Assert.True(frame.Height > 1);
        Assert.True(frame.RgbaBytes.Length >= frame.Height * frame.Stride);
    }

    private static string ResolveWindowsSliceWorkspacePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "examples", "workspaces", "windows-slice");
            if (File.Exists(Path.Combine(candidate, "dearstory.toml")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The Windows slice workspace could not be resolved from the integration test base directory.");
    }
}
