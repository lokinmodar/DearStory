using DearStory.Core;

namespace DearStory.Catalog;

/// <summary>
/// Builds the searchable catalog tree shown by the Windows catalog shell.
/// </summary>
public static class CatalogTreeBuilder
{
    /// <summary>
    /// Builds a stable tree from the supplied story descriptors.
    /// </summary>
    /// <param name="stories">The stories to group into the catalog tree.</param>
    /// <returns>The root node that owns the grouped catalog hierarchy.</returns>
    public static CatalogTreeNode Build(IEnumerable<StoryDescriptor> stories)
    {
        ArgumentNullException.ThrowIfNull(stories);

        var root = new MutableCatalogTreeNode("Stories");

        foreach (var story in stories.OrderBy(static item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            var segments = GetSegments(story);
            var cursor = root;

            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var isLeaf = index == segments.Count - 1;
                cursor = cursor.GetOrAddChild(segment, isLeaf ? story : null);
            }
        }

        return root.ToImmutable();
    }

    private static IReadOnlyList<string> GetSegments(StoryDescriptor story)
    {
        if (story.Hierarchy.Count > 0)
        {
            return story.Hierarchy;
        }

        return story.Title
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static segment => segment.Length == 0 ? "Untitled" : segment)
            .ToArray();
    }

    private sealed class MutableCatalogTreeNode
    {
        private readonly Dictionary<string, MutableCatalogTreeNode> _children = new(StringComparer.OrdinalIgnoreCase);

        public MutableCatalogTreeNode(string title, StoryDescriptor? story = null)
        {
            Title = title;
            Story = story;
        }

        public string Title { get; }

        public StoryDescriptor? Story { get; private set; }

        public MutableCatalogTreeNode GetOrAddChild(string title, StoryDescriptor? story)
        {
            if (!_children.TryGetValue(title, out var child))
            {
                child = new MutableCatalogTreeNode(title, story);
                _children.Add(title, child);
            }
            else if (story is not null)
            {
                child.Story = story;
            }

            return child;
        }

        public CatalogTreeNode ToImmutable() =>
            new(
                Title,
                Story,
                _children.Values
                    .OrderBy(static child => child.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(static child => child.ToImmutable())
                    .ToArray());
    }
}

/// <summary>
/// Represents one node in the unified DearStory catalog tree.
/// </summary>
/// <param name="Title">The display title for the node.</param>
/// <param name="Story">The optional story descriptor when the node is a leaf.</param>
/// <param name="Children">The ordered child nodes.</param>
public sealed record CatalogTreeNode(string Title, StoryDescriptor? Story, IReadOnlyList<CatalogTreeNode> Children)
{
    /// <summary>
    /// Gets the flattened search text used by the catalog filter.
    /// </summary>
    /// <value>The search text composed from the node title and canonical story ID.</value>
    public string SearchText => Story is null ? Title : $"{Title} {Story.Id.Value}";
}
