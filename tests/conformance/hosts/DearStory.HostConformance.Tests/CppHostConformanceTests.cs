using System.Runtime.Versioning;
using Xunit;

namespace DearStory.HostConformance.Tests;

[SupportedOSPlatform("windows")]
public sealed class CppHostConformanceTests
{
    [Fact(Timeout = 30_000)]
    public async Task Cpp_host_publishes_story_index_and_first_frame()
    {
        await using var harness = await HostHarness.StartAsync("cpp-host");

        var stories = await harness.WaitForStoryIndexAsync();
        var frame = await harness.OpenSessionAndReadFrameAsync("buttons/primary");

        Assert.Contains(stories, story => story.CanonicalId == "buttons/primary");
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }
}
