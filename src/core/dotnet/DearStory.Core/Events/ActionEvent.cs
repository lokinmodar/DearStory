using System.Text.Json.Nodes;

namespace DearStory.Core.Events;

/// <summary>
/// Represents one emitted story action event.
/// </summary>
public sealed record ActionEvent
{
    /// <summary>
    /// Gets the stable action name.
    /// </summary>
    /// <value>The action name.</value>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the serializable action payload.
    /// </summary>
    /// <value>The serializable action payload.</value>
    public required JsonNode Payload { get; init; }

    /// <summary>
    /// Gets the UTC time when the action was emitted.
    /// </summary>
    /// <value>The UTC emission time.</value>
    public required DateTimeOffset EmittedAtUtc { get; init; }

    /// <summary>
    /// Gets the optional associated target identifier.
    /// </summary>
    /// <value>The optional target identifier.</value>
    public string? TargetId { get; init; }
}
