using System.Text.Json.Nodes;

namespace DearStory.Core.Events;

/// <summary>
/// Represents one emitted story log event.
/// </summary>
public sealed record LogEvent
{
    /// <summary>
    /// Gets the log level.
    /// </summary>
    /// <value>The log level.</value>
    public required string Level { get; init; }

    /// <summary>
    /// Gets the log message text.
    /// </summary>
    /// <value>The log message text.</value>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the UTC time when the log was emitted.
    /// </summary>
    /// <value>The UTC emission time.</value>
    public required DateTimeOffset EmittedAtUtc { get; init; }

    /// <summary>
    /// Gets the optional structured log details.
    /// </summary>
    /// <value>The optional structured log details.</value>
    public JsonNode? Details { get; init; }
}
