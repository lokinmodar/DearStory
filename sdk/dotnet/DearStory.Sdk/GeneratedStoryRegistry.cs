using System.Text.Json.Nodes;
using DearStory.Core;
using DearStory.Core.Schemas;

namespace DearStory.Sdk;

/// <summary>
/// Represents one DearStory story callback signature.
/// </summary>
/// <param name="context">The active story context.</param>
public delegate void StoryCallback(StoryContext context);

/// <summary>
/// Describes one serializable story argument entry.
/// </summary>
public sealed record GeneratedStoryArgument
{
    /// <summary>
    /// Gets the stable argument name.
    /// </summary>
    /// <value>The stable argument name.</value>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the schema fragment for the argument.
    /// </summary>
    /// <value>The schema fragment for the argument.</value>
    public required JsonNode Schema { get; init; }

    /// <summary>
    /// Gets the default serialized value for the argument.
    /// </summary>
    /// <value>The default serialized value for the argument.</value>
    public JsonNode? DefaultValue { get; init; }

    /// <summary>
    /// Gets the optional human-readable description.
    /// </summary>
    /// <value>The optional human-readable description.</value>
    public string? Description { get; init; }
}

/// <summary>
/// Stores one generated or reflected DearStory story registration.
/// </summary>
public sealed record GeneratedStoryRegistration
{
    /// <summary>
    /// Gets the canonical story descriptor.
    /// </summary>
    /// <value>The canonical story descriptor.</value>
    public required StoryDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets the parsed DearStory argument schema.
    /// </summary>
    /// <value>The parsed DearStory argument schema.</value>
    public required ArgumentSchema ArgumentSchema { get; init; }

    /// <summary>
    /// Gets the default serialized argument snapshot.
    /// </summary>
    /// <value>The default serialized argument snapshot.</value>
    public required JsonNode DefaultArguments { get; init; }

    /// <summary>
    /// Gets the per-argument metadata entries.
    /// </summary>
    /// <value>The per-argument metadata entries.</value>
    public required IReadOnlyList<GeneratedStoryArgument> Arguments { get; init; }

    /// <summary>
    /// Gets the story callback delegate.
    /// </summary>
    /// <value>The story callback delegate.</value>
    public required StoryCallback Render { get; init; }
}

/// <summary>
/// Represents the generated or reflected DearStory story registry.
/// </summary>
public sealed partial class GeneratedStoryRegistry
{
    /// <summary>
    /// Gets the generated or reflected story registrations.
    /// </summary>
    /// <value>The generated or reflected story registrations.</value>
    public required IReadOnlyList<GeneratedStoryRegistration> Registrations { get; init; }

    /// <summary>
    /// Gets the story descriptors sorted by canonical story ID.
    /// </summary>
    /// <value>The story descriptors sorted by canonical story ID.</value>
    public IReadOnlyList<StoryDescriptor> Descriptors =>
        Registrations
            .Select(static registration => registration.Descriptor)
            .OrderBy(static descriptor => descriptor.Id.Value, StringComparer.Ordinal)
            .ToArray();
}
