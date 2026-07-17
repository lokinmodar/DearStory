using System.Text.Json.Nodes;
using DearStory.Core.Services;

namespace DearStory.Core.Sessions;

/// <summary>
/// Represents one active DearStory story session.
/// </summary>
public sealed class StorySession
{
    private StorySession()
    {
    }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    /// <value>The session identifier.</value>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Gets the canonical story identifier.
    /// </summary>
    /// <value>The canonical story identifier.</value>
    public required StoryId StoryId { get; init; }

    /// <summary>
    /// Gets or sets the active serializable arguments.
    /// </summary>
    /// <value>The active serializable arguments.</value>
    public required JsonNode Arguments { get; set; }

    /// <summary>
    /// Gets the deterministic clock for the session.
    /// </summary>
    /// <value>The deterministic clock.</value>
    public required DeterministicClock Clock { get; init; }

    /// <summary>
    /// Gets the deterministic random source for the session.
    /// </summary>
    /// <value>The deterministic random source.</value>
    public required DeterministicRandom Random { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the session is closed.
    /// </summary>
    /// <value><see langword="true" /> if the session is closed; otherwise, <see langword="false" />.</value>
    public bool IsClosed { get; private set; }

    /// <summary>
    /// Gets the optional close reason.
    /// </summary>
    /// <value>The optional close reason.</value>
    public string? CloseReason { get; private set; }

    /// <summary>
    /// Opens a new story session with deterministic services.
    /// </summary>
    /// <param name="sessionId">A session identifier.</param>
    /// <param name="storyId">A canonical story identifier.</param>
    /// <param name="initialArguments">An initial serializable argument snapshot.</param>
    /// <param name="seed">A deterministic seed value.</param>
    /// <param name="startTimeUtc">A deterministic UTC start time.</param>
    /// <returns>A new active story session.</returns>
    public static StorySession Open(
        Guid sessionId,
        StoryId storyId,
        JsonNode initialArguments,
        long seed,
        DateTimeOffset startTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(initialArguments);

        return new StorySession
        {
            SessionId = sessionId,
            StoryId = storyId,
            Arguments = initialArguments.DeepClone(),
            Clock = new DeterministicClock(startTimeUtc),
            Random = new DeterministicRandom(seed),
        };
    }

    /// <summary>
    /// Resets the session to requested default arguments and deterministic services.
    /// </summary>
    /// <param name="defaultArguments">A default serializable argument snapshot.</param>
    /// <param name="seed">A deterministic seed value.</param>
    /// <param name="startTimeUtc">A deterministic UTC start time.</param>
    public void Reset(JsonNode defaultArguments, long seed, DateTimeOffset startTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(defaultArguments);

        Arguments = defaultArguments.DeepClone();
        Clock.Reset(startTimeUtc);
        Random.Reset(seed);
        IsClosed = false;
        CloseReason = null;
    }

    /// <summary>
    /// Closes the session with an optional reason.
    /// </summary>
    /// <param name="reason">An optional close reason.</param>
    public void Close(string? reason = null)
    {
        IsClosed = true;
        CloseReason = reason;
    }
}
