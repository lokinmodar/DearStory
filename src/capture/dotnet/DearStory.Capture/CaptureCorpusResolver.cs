using DearStory.Core;

namespace DearStory.Capture;

/// <summary>
/// Resolves the canonical visual corpus from story metadata and workspace-level overrides.
/// </summary>
public static class CaptureCorpusResolver
{
    /// <summary>
    /// Resolves the set of stories that belong to the canonical visual corpus.
    /// </summary>
    /// <param name="stories">The available story descriptors.</param>
    /// <param name="overrides">The normalized or raw story-ID overrides that opt stories in or out of the canonical corpus.</param>
    /// <returns>The canonical-corpus stories ordered by canonical story identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stories" /> or <paramref name="overrides" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">An override contains a story identifier that is empty or whitespace.</exception>
    public static IReadOnlyList<StoryDescriptor> ResolveCanonicalStories(
        IEnumerable<StoryDescriptor> stories,
        IEnumerable<KeyValuePair<string, bool>> overrides)
    {
        ArgumentNullException.ThrowIfNull(stories);
        ArgumentNullException.ThrowIfNull(overrides);

        var overrideMap = overrides.ToDictionary(
            static entry => StoryId.Parse(entry.Key).Value,
            static entry => entry.Value,
            StringComparer.Ordinal);

        return stories
            .Where(story => overrideMap.TryGetValue(story.Id.Value, out var include)
                ? include
                : story.Visual.IncludeInCanonicalCorpus)
            .OrderBy(static story => story.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
