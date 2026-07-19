using DearStory.Runner.Configuration;

namespace DearStory.Runner.Capture;

/// <summary>
/// Resolves managed and native DearStory host executable paths for one build configuration.
/// </summary>
internal static class HostArtifactPathResolver
{
    /// <summary>
    /// Resolves the requested build configuration for the active capture process.
    /// </summary>
    /// <param name="explicitConfiguration">The optional explicit configuration requested by the caller.</param>
    /// <returns>The effective build configuration.</returns>
    internal static string ResolveBuildConfiguration(string? explicitConfiguration) =>
        BuildConfigurationResolver.Resolve(explicitConfiguration, AppContext.BaseDirectory);

    /// <summary>
    /// Resolves the native host executable path for the requested configuration.
    /// </summary>
    /// <param name="repositoryRoot">The repository root that contains the native host artifacts.</param>
    /// <param name="buildConfiguration">The build configuration to resolve.</param>
    /// <returns>The absolute path to the native host executable.</returns>
    internal static string ResolveNativeHostExecutable(string repositoryRoot, string buildConfiguration) =>
        Path.Combine(repositoryRoot, "artifacts", "bin", "native", buildConfiguration, "dearstory-host-cpp.exe");

    /// <summary>
    /// Resolves the managed host executable path for the requested configuration.
    /// </summary>
    /// <param name="repositoryRoot">The repository root that contains the managed host artifacts.</param>
    /// <param name="buildConfiguration">The build configuration to resolve.</param>
    /// <returns>The absolute path to the managed host executable.</returns>
    internal static string ResolveManagedHostExecutable(string repositoryRoot, string buildConfiguration) =>
        Path.Combine(
            repositoryRoot,
            "src",
            "hosts",
            "dotnet",
            "DearStory.Host",
            "bin",
            buildConfiguration,
            "net10.0",
            "DearStory.Host.exe");
}
