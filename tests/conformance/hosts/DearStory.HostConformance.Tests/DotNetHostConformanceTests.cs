using System.Runtime.Versioning;
using Xunit;

namespace DearStory.HostConformance.Tests;

[SupportedOSPlatform("windows")]
public sealed class DotNetHostConformanceTests
{
    [Fact(Timeout = 30_000)]
    public async Task Dotnet_host_publishes_story_index_and_first_frame()
    {
        await using var harness = await HostHarness.StartAsync("dotnet-host");

        var stories = await harness.WaitForStoryIndexAsync();
        var frame = await harness.OpenSessionAndReadFrameAsync("buttons/primarymanaged");

        Assert.Contains(stories, story => story.CanonicalId == "buttons/primarymanaged");
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }
}
