using DearStory.Core;

namespace DearStory.Docs.Autodocs;

/// <summary>
/// Generates lightweight autodoc metadata from DearStory story descriptors.
/// </summary>
public sealed class AutodocsGenerator
{
    /// <summary>
    /// Generates autodoc entries for the supplied stories.
    /// </summary>
    /// <param name="stories">The stories to describe.</param>
    /// <returns>The generated autodoc entries.</returns>
    public IReadOnlyList<AutodocEntry> Generate(IEnumerable<StoryDescriptor> stories)
    {
        ArgumentNullException.ThrowIfNull(stories);

        return stories
            .OrderBy(static story => story.Id.Value, StringComparer.OrdinalIgnoreCase)
            .Select(
                static story => new AutodocEntry(
                    story.Id.Value,
                    story.Title,
                    story.Description ?? string.Empty))
            .ToArray();
    }
}

/// <summary>
/// Represents one generated autodoc entry.
/// </summary>
/// <param name="StoryId">The canonical story identifier.</param>
/// <param name="Title">The story title.</param>
/// <param name="Description">The optional story description.</param>
public sealed record AutodocEntry(string StoryId, string Title, string Description);
