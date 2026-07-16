namespace DearStory.Core;

/// <summary>
/// Maintains the merged DearStory story catalog across hosts.
/// </summary>
public sealed class StoryCatalog
{
    private readonly Dictionary<StoryId, StoryDescriptor> _stories = [];
    private readonly Dictionary<StoryId, string> _storyHosts = [];

    /// <summary>
    /// Merges one host's published stories into the catalog.
    /// </summary>
    /// <param name="hostId">A host identifier.</param>
    /// <param name="stories">The stories published by the host.</param>
    /// <returns>A merge result describing the updated catalog state.</returns>
    public CatalogMergeResult Merge(string hostId, IReadOnlyList<StoryDescriptor> stories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentNullException.ThrowIfNull(stories);

        var diagnostics = new List<CatalogDiagnostic>();

        foreach (var story in stories)
        {
            if (_storyHosts.TryGetValue(story.Id, out var existingHost) &&
                !string.Equals(existingHost, hostId, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    new CatalogDiagnostic
                    {
                        Code = "story.duplicate_id",
                        Message = $"The story '{story.Id.Value}' is already published by host '{existingHost}'.",
                        HostId = hostId,
                        StoryId = story.Id,
                    });
                continue;
            }

            _stories[story.Id] = story;
            _storyHosts[story.Id] = hostId;
        }

        return new CatalogMergeResult
        {
            Succeeded = diagnostics.Count == 0,
            Stories = _stories.Values.OrderBy(static item => item.Id.Value, StringComparer.Ordinal).ToArray(),
            Diagnostics = diagnostics,
        };
    }
}
