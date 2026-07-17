namespace DearStory.Docs.Markdown;

/// <summary>
/// Represents one parsed Markdown document used by the DearStory static docs pipeline.
/// </summary>
public sealed class MarkdownDocumentModel
{
    /// <summary>
    /// Gets the parsed document title.
    /// </summary>
    /// <value>The parsed document title.</value>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the original Markdown source text.
    /// </summary>
    /// <value>The original Markdown source text.</value>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the parsed doc blocks in source order.
    /// </summary>
    /// <value>The parsed doc blocks in source order.</value>
    public required IReadOnlyList<MarkdownDocBlock> Blocks { get; init; }
}

/// <summary>
/// Represents one typed doc block extracted from a Markdown document.
/// </summary>
public sealed class MarkdownDocBlock
{
    /// <summary>
    /// Gets the stable doc-block kind.
    /// </summary>
    /// <value>The stable doc-block kind.</value>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the parsed attribute map for the block.
    /// </summary>
    /// <value>The parsed attribute map for the block.</value>
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
}
