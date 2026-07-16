using System.Text.Json.Nodes;
using DearStory.Core.Sessions;
using DearStory.Core.Services;
using Xunit;

namespace DearStory.Core.Tests;

public sealed class StorySessionTests
{
    [Fact]
    public void Open_creates_active_session_with_initial_state()
    {
        var defaultArguments = JsonNode.Parse("""{"label":"Save"}""")!;
        var session = StorySession.Open(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            StoryId.Parse("buttons/primary"),
            defaultArguments,
            42,
            DateTimeOffset.Parse("2026-07-16T12:00:00Z"));

        Assert.Equal("buttons/primary", session.StoryId.Value);
        Assert.Equal("Save", session.Arguments["label"]!.GetValue<string>());
        Assert.False(session.IsClosed);
    }

    [Fact]
    public void Reset_restores_default_arguments_and_deterministic_services()
    {
        var defaultArguments = JsonNode.Parse("""{"label":"Save"}""")!;
        var session = StorySession.Open(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            StoryId.Parse("buttons/primary"),
            JsonNode.Parse("""{"label":"Discard"}""")!,
            7,
            DateTimeOffset.Parse("2026-07-16T12:10:00Z"));

        session.Arguments["label"] = "Discard";
        session.Clock.Advance(TimeSpan.FromSeconds(30));
        _ = session.Random.NextUInt32();

        session.Reset(defaultArguments, 42, DateTimeOffset.Parse("2026-07-16T12:00:00Z"));

        Assert.Equal("Save", session.Arguments["label"]!.GetValue<string>());
        Assert.Equal(DateTimeOffset.Parse("2026-07-16T12:00:00Z"), session.Clock.CurrentUtc);
        Assert.Equal(new DeterministicRandom(42).NextUInt32(), session.Random.NextUInt32());
    }

    [Fact]
    public void Close_marks_session_closed()
    {
        var session = StorySession.Open(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            StoryId.Parse("buttons/primary"),
            JsonNode.Parse("""{"label":"Save"}""")!,
            1,
            DateTimeOffset.Parse("2026-07-16T12:00:00Z"));

        session.Close("completed");

        Assert.True(session.IsClosed);
        Assert.Equal("completed", session.CloseReason);
    }
}
