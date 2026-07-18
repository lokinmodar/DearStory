namespace DearStory.Runner.Configuration;

/// <summary>Represents the DearStory workspace file consumed by the Windows runner.</summary>
public sealed class WorkspaceConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="WorkspaceConfiguration" /> class.</summary>
    /// <param name="workspace">The required workspace metadata.</param>
    /// <param name="catalog">The optional catalog display settings.</param>
    /// <param name="visual">The visual-story overrides loaded from the workspace file.</param>
    /// <param name="hosts">The ordered host definitions available to the runner.</param>
    /// <param name="docs">The documentation globs available to the runner.</param>
    public WorkspaceConfiguration(
        WorkspaceDescriptor workspace,
        CatalogConfiguration catalog,
        VisualConfiguration visual,
        IReadOnlyList<HostConfiguration> hosts,
        IReadOnlyList<DocumentationSource> docs)
    {
        Workspace = workspace;
        Catalog = catalog;
        Visual = visual;
        Hosts = hosts;
        Docs = docs;
    }

    /// <summary>Gets the workspace metadata.</summary>
    /// <value>The required workspace descriptor loaded from <c>dearstory.toml</c>.</value>
    public WorkspaceDescriptor Workspace { get; }

    /// <summary>Gets the catalog presentation settings.</summary>
    /// <value>The catalog settings loaded from the workspace file.</value>
    public CatalogConfiguration Catalog { get; }

    /// <summary>Gets the visual-story overrides.</summary>
    /// <value>The visual configuration loaded from the workspace file.</value>
    public VisualConfiguration Visual { get; }

    /// <summary>Gets the ordered host definitions.</summary>
    /// <value>The host definitions the runner should supervise.</value>
    public IReadOnlyList<HostConfiguration> Hosts { get; }

    /// <summary>Gets the documentation source globs.</summary>
    /// <value>The workspace documentation sources to include in static output.</value>
    public IReadOnlyList<DocumentationSource> Docs { get; }
}

/// <summary>Represents the top-level workspace identity and resolved root path.</summary>
public sealed class WorkspaceDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="WorkspaceDescriptor" /> class.</summary>
    /// <param name="name">The human-readable workspace name.</param>
    /// <param name="rootPath">The resolved absolute workspace root path.</param>
    public WorkspaceDescriptor(string name, string rootPath)
    {
        Name = name;
        RootPath = rootPath;
    }

    /// <summary>Gets the human-readable workspace name.</summary>
    /// <value>The workspace name from the <c>[workspace]</c> section.</value>
    public string Name { get; }

    /// <summary>Gets the resolved absolute workspace root path.</summary>
    /// <value>The absolute directory that owns the workspace file.</value>
    public string RootPath { get; }
}

/// <summary>Represents the catalog presentation settings loaded from a workspace file.</summary>
public sealed class CatalogConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="CatalogConfiguration" /> class.</summary>
    /// <param name="theme">The catalog theme identifier.</param>
    public CatalogConfiguration(string theme)
    {
        Theme = theme;
    }

    /// <summary>Gets the catalog theme identifier.</summary>
    /// <value>The configured theme name. The default is <c>dark</c>.</value>
    public string Theme { get; }
}

/// <summary>Represents visual-story overrides loaded from a workspace file.</summary>
public sealed class VisualConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="VisualConfiguration" /> class.</summary>
    /// <param name="overrides">The visual-story override entries.</param>
    public VisualConfiguration(IReadOnlyList<VisualStoryOverride> overrides)
    {
        Overrides = overrides;
    }

    /// <summary>Gets the visual-story override entries.</summary>
    /// <value>The ordered list of configured visual overrides.</value>
    public IReadOnlyList<VisualStoryOverride> Overrides { get; }
}

/// <summary>Represents one visual-story override entry from the workspace file.</summary>
public sealed class VisualStoryOverride
{
    /// <summary>Initializes a new instance of the <see cref="VisualStoryOverride" /> class.</summary>
    /// <param name="storyId">The story identifier the override applies to.</param>
    /// <param name="includeInCanonicalCorpus"><see langword="true" /> to include the story in the canonical corpus; otherwise, <see langword="false" />.</param>
    public VisualStoryOverride(string storyId, bool includeInCanonicalCorpus)
    {
        StoryId = storyId;
        IncludeInCanonicalCorpus = includeInCanonicalCorpus;
    }

    /// <summary>Gets the story identifier the override applies to.</summary>
    /// <value>The configured story identifier.</value>
    public string StoryId { get; }

    /// <summary>Gets a value that indicates whether the story is included in the canonical corpus.</summary>
    /// <value><see langword="true" /> if the story is included in the canonical corpus; otherwise, <see langword="false" />.</value>
    public bool IncludeInCanonicalCorpus { get; }
}

/// <summary>Represents one host entry from the workspace file.</summary>
public sealed class HostConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="HostConfiguration" /> class.</summary>
    /// <param name="id">The stable host identifier.</param>
    /// <param name="builder">The builder kind used for the host.</param>
    /// <param name="project">The optional project selector for the host.</param>
    public HostConfiguration(string id, string builder, string? project)
    {
        Id = id;
        Builder = builder;
        Project = project;
    }

    /// <summary>Gets the stable host identifier.</summary>
    /// <value>The unique host identifier used throughout the workspace.</value>
    public string Id { get; }

    /// <summary>Gets the host builder kind.</summary>
    /// <value>The build pipeline identifier, such as <c>cmake</c> or <c>dotnet</c>.</value>
    public string Builder { get; }

    /// <summary>Gets the optional project selector.</summary>
    /// <value>The host project selector, or <see langword="null" /> when the workspace omits it.</value>
    public string? Project { get; }
}

/// <summary>Represents one documentation glob from the workspace file.</summary>
public sealed class DocumentationSource
{
    /// <summary>Initializes a new instance of the <see cref="DocumentationSource" /> class.</summary>
    /// <param name="glob">The glob used to discover Markdown documentation files.</param>
    public DocumentationSource(string glob)
    {
        Glob = glob;
    }

    /// <summary>Gets the documentation glob.</summary>
    /// <value>The glob pattern used to discover Markdown sources.</value>
    public string Glob { get; }
}
