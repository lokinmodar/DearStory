namespace DearStory.Core;

/// <summary>
/// Represents the result of merging one host's story publication into the catalog.
/// </summary>
public sealed record CatalogMergeResult
{
    /// <summary>
    /// Gets a value that indicates whether the merge completed without diagnostics.
    /// </summary>
    /// <value><see langword="true" /> if the merge succeeded; otherwise, <see langword="false" />.</value>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the merged catalog stories sorted by canonical ID.
    /// </summary>
    /// <value>The merged story list.</value>
    public required IReadOnlyList<StoryDescriptor> Stories { get; init; }

    /// <summary>
    /// Gets the diagnostics produced while merging.
    /// </summary>
    /// <value>The merge diagnostics.</value>
    public required IReadOnlyList<CatalogDiagnostic> Diagnostics { get; init; }
}

/// <summary>
/// Represents one catalog merge diagnostic.
/// </summary>
public sealed record CatalogDiagnostic
{
    /// <summary>
    /// Gets the stable diagnostic code.
    /// </summary>
    /// <value>The diagnostic code.</value>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable diagnostic message.
    /// </summary>
    /// <value>The diagnostic message.</value>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the host that attempted the conflicting publication.
    /// </summary>
    /// <value>The publishing host identifier.</value>
    public required string HostId { get; init; }

    /// <summary>
    /// Gets the canonical story identifier associated with the diagnostic.
    /// </summary>
    /// <value>The canonical story identifier.</value>
    public required StoryId StoryId { get; init; }
}
