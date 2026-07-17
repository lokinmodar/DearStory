namespace DearStory.Core;

/// <summary>
/// Declares one story's visual-capture policy.
/// </summary>
public sealed record StoryVisualDescriptor
{
    /// <summary>
    /// Gets the default visual policy for ordinary stories.
    /// </summary>
    /// <value>The default visual policy.</value>
    public static StoryVisualDescriptor Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether this story supports RGBA capture.
    /// </summary>
    /// <value><see langword="true" /> when the story supports RGBA capture.</value>
    public bool SupportsCapture { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether this story opts into the canonical visual corpus.
    /// </summary>
    /// <value><see langword="true" /> when the story is part of the canonical visual corpus.</value>
    public bool IncludeInCanonicalCorpus { get; init; }
}
