using System.Reflection;
using System.Runtime.Loader;

namespace DearStory.Host;

/// <summary>
/// Loads one managed story assembly into a collectible context for Windows-host isolation experiments.
/// </summary>
/// <remarks>
/// The current baseline still relies on process restart for reliable hot reload. The collectible load context
/// keeps the assembly boundary explicit so later slices can expand the reload strategy without rewriting host startup.
/// </remarks>
internal sealed class StoryAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _assemblyPath;
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoryAssemblyLoadContext" /> class.
    /// </summary>
    /// <param name="assemblyPath">The absolute path of the story assembly to load.</param>
    public StoryAssemblyLoadContext(string assemblyPath)
        : base(isCollectible: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        _assemblyPath = assemblyPath;
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    /// <summary>
    /// Loads the configured story assembly into this collectible context.
    /// </summary>
    /// <returns>The loaded story assembly.</returns>
    public Assembly LoadStoryAssembly() => LoadFromAssemblyPath(_assemblyPath);

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
