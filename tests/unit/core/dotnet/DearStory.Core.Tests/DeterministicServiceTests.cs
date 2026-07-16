using DearStory.Core.Services;
using Xunit;

namespace DearStory.Core.Tests;

public sealed class DeterministicServiceTests
{
    [Fact]
    public void DeterministicClock_advances_only_when_requested()
    {
        var clock = new DeterministicClock(DateTimeOffset.Parse("2026-07-16T12:00:00Z"));

        clock.Advance(TimeSpan.FromMilliseconds(250));

        Assert.Equal(DateTimeOffset.Parse("2026-07-16T12:00:00.250+00:00"), clock.CurrentUtc);
    }

    [Fact]
    public void DeterministicRandom_replays_the_same_sequence_after_reset()
    {
        var random = new DeterministicRandom(42);
        var first = random.NextUInt32();
        var second = random.NextUInt32();

        random.Reset(42);

        Assert.Equal(first, random.NextUInt32());
        Assert.Equal(second, random.NextUInt32());
    }
}
