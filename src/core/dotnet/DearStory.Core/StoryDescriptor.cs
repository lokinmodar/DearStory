namespace DearStory.Core;

/// <summary>
/// Describes one published DearStory story.
/// </summary>
public sealed record StoryDescriptor
{
    /// <summary>
    /// Gets the canonical story identifier.
    /// </summary>
    /// <value>The canonical story identifier.</value>
    public required StoryId Id { get; init; }

    /// <summary>
    /// Gets the human-facing story title.
    /// </summary>
    /// <value>The display title.</value>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the story hierarchy segments.
    /// </summary>
    /// <value>The hierarchy segments. The default is an empty list.</value>
    public IReadOnlyList<string> Hierarchy { get; init; } = [];

    /// <summary>
    /// Gets the story tags.
    /// </summary>
    /// <value>The tag values. The default is an empty list.</value>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the optional story description.
    /// </summary>
    /// <value>The optional description text.</value>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional source-path hint for the story.
    /// </summary>
    /// <value>The optional source-path hint.</value>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Gets the story capability strings.
    /// </summary>
    /// <value>The capability strings. The default is an empty list.</value>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// Gets the visual regression metadata for the story.
    /// </summary>
    /// <value>The visual regression metadata. The default is <see cref="StoryVisualDescriptor.Default" />.</value>
    public StoryVisualDescriptor Visual { get; init; } = StoryVisualDescriptor.Default;

    /// <summary>
    /// Creates a minimal story descriptor from a raw ID and title.
    /// </summary>
    /// <param name="id">A raw story identifier.</param>
    /// <param name="title">A display title.</param>
    /// <returns>A canonical story descriptor.</returns>
    public static StoryDescriptor Create(string id, string title) =>
        new()
        {
            Id = StoryId.Parse(id),
            Title = title,
        };
}
