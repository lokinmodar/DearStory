using Tomlyn;
using Tomlyn.Model;

namespace DearStory.Runner.Configuration;

/// <summary>Loads and binds DearStory workspace files for the Windows runner.</summary>
public static class WorkspaceConfigurationLoader
{
    private const string WorkspaceFileName = "dearstory.toml";

    /// <summary>Finds and loads the nearest DearStory workspace file from the supplied path.</summary>
    /// <param name="startDirectory">The workspace directory or the path to a <c>dearstory.toml</c> file.</param>
    /// <returns>The bound workspace configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="startDirectory" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A DearStory workspace file cannot be found or the TOML document is invalid.</exception>
    public static WorkspaceConfiguration Load(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            throw new ArgumentException("A workspace path must be provided.", nameof(startDirectory));
        }

        var workspaceFilePath = ResolveWorkspaceFilePath(startDirectory);
        var workspaceRootPath = Path.GetDirectoryName(workspaceFilePath)
            ?? throw new InvalidOperationException("The DearStory workspace path must have a parent directory.");

        return LoadFromText(File.ReadAllText(workspaceFilePath), workspaceRootPath);
    }

    /// <summary>Loads one DearStory workspace from raw TOML text.</summary>
    /// <param name="tomlText">The workspace TOML document to bind.</param>
    /// <returns>The bound workspace configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="tomlText" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The TOML document is invalid or missing required sections.</exception>
    public static WorkspaceConfiguration LoadFromText(string tomlText)
    {
        return LoadFromText(tomlText, Directory.GetCurrentDirectory());
    }

    internal static WorkspaceConfiguration LoadFromText(string tomlText, string workspaceRootPath)
    {
        if (string.IsNullOrWhiteSpace(tomlText))
        {
            throw new ArgumentException("The DearStory TOML text must be provided.", nameof(tomlText));
        }

        TomlTable? root;
        try
        {
            root = Tomlyn.TomlSerializer.Deserialize<TomlTable>(tomlText, Tomlyn.TomlSerializerOptions.Default);
        }
        catch (Exception exception) when (exception is not InvalidOperationException && exception is not ArgumentException)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }

        if (root is null)
        {
            throw new InvalidOperationException("The DearStory TOML document could not be parsed.");
        }

        return Bind(root, workspaceRootPath);
    }

    private static WorkspaceConfiguration Bind(TomlTable root, string workspaceRootPath)
    {
        var workspaceTable = GetRequiredTable(root, "workspace");
        var workspace = new WorkspaceDescriptor(
            GetRequiredString(workspaceTable, "name", "[workspace]"),
            Path.GetFullPath(workspaceRootPath));

        var catalog = root.TryGetValue("catalog", out var catalogValue) && catalogValue is TomlTable catalogTable
            ? new CatalogConfiguration(GetOptionalString(catalogTable, "theme") ?? "dark")
            : new CatalogConfiguration("dark");

        var hosts = BindHosts(root);
        var docs = BindDocs(root);

        return new WorkspaceConfiguration(workspace, catalog, hosts, docs);
    }

    private static IReadOnlyList<HostConfiguration> BindHosts(TomlTable root)
    {
        if (root.TryGetValue("hosts", out var hostsValue) is false || hostsValue is not TomlTableArray hostsArray)
        {
            return Array.Empty<HostConfiguration>();
        }

        var hosts = new List<HostConfiguration>(hostsArray.Count);
        foreach (var entry in hostsArray.OfType<TomlTable>())
        {
            hosts.Add(
                new HostConfiguration(
                    GetRequiredString(entry, "id", "[[hosts]]"),
                    GetRequiredString(entry, "builder", "[[hosts]]"),
                    GetOptionalString(entry, "project")));
        }

        return hosts;
    }

    private static IReadOnlyList<DocumentationSource> BindDocs(TomlTable root)
    {
        if (root.TryGetValue("docs", out var docsValue) is false || docsValue is not TomlTableArray docsArray)
        {
            return Array.Empty<DocumentationSource>();
        }

        var docs = new List<DocumentationSource>(docsArray.Count);
        foreach (var entry in docsArray.OfType<TomlTable>())
        {
            docs.Add(new DocumentationSource(GetRequiredString(entry, "glob", "[[docs]]")));
        }

        return docs;
    }

    private static string ResolveWorkspaceFilePath(string startDirectory)
    {
        var fullPath = Path.GetFullPath(startDirectory);
        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetFileName(fullPath), WorkspaceFileName, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            throw new InvalidOperationException($"The file '{fullPath}' is not a {WorkspaceFileName} workspace file.");
        }

        var directory = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? fullPath);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, WorkspaceFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"A {WorkspaceFileName} file could not be found starting from '{fullPath}'.");
    }

    private static TomlTable GetRequiredTable(TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is TomlTable nestedTable)
        {
            return nestedTable;
        }

        throw new InvalidOperationException($"The '{key}' table is required in the DearStory workspace file.");
    }

    private static string GetRequiredString(TomlTable table, string key, string sectionName)
    {
        var value = GetOptionalString(table, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"The '{key}' value is required in {sectionName}.");
        }

        return value;
    }

    private static string? GetOptionalString(TomlTable table, string key)
    {
        return table.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
