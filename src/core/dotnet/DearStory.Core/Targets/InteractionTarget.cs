using System.Text.Json.Nodes;

namespace DearStory.Core.Targets;

/// <summary>
/// Represents one named interaction target reported by a story.
/// </summary>
public sealed record InteractionTarget
{
    /// <summary>
    /// Gets the stable target identifier.
    /// </summary>
    /// <value>The stable target identifier.</value>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the optional serialized bounds payload.
    /// </summary>
    /// <value>The optional serialized bounds payload.</value>
    public JsonNode? Bounds { get; init; }

    /// <summary>
    /// Gets the optional semantic metadata.
    /// </summary>
    /// <value>The optional semantic metadata.</value>
    public InteractionTargetSemanticMetadata? Semantic { get; init; }
}

/// <summary>
/// Represents optional semantic metadata for one interaction target.
/// </summary>
public sealed record InteractionTargetSemanticMetadata
{
    /// <summary>
    /// Gets the optional semantic role.
    /// </summary>
    /// <value>The optional semantic role.</value>
    public string? Role { get; init; }

    /// <summary>
    /// Gets the optional accessible name.
    /// </summary>
    /// <value>The optional accessible name.</value>
    public string? AccessibleName { get; init; }

    /// <summary>
    /// Gets the optional semantic description.
    /// </summary>
    /// <value>The optional semantic description.</value>
    public string? Description { get; init; }
}
