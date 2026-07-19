namespace DearStory.Testing;

/// <summary>
/// Resolves the active Debug or Release configuration for DearStory test processes.
/// </summary>
internal static class CurrentBuildConfiguration
{
    private const string TestConfigurationEnvironmentVariable = "DEARSTORY_TEST_CONFIGURATION";

    /// <summary>
    /// Gets the current build configuration for the active test process.
    /// </summary>
    /// <returns>
    /// <c>Release</c> when the <c>DEARSTORY_TEST_CONFIGURATION</c> environment variable is set to
    /// <c>Release</c>; <c>Debug</c> when the environment variable is set to <c>Debug</c>; otherwise,
    /// the nearest configuration segment inferred from <see cref="AppContext.BaseDirectory" /> with a
    /// final fallback to <c>Debug</c>.
    /// </returns>
    internal static string CurrentConfiguration() => ResolveConfiguration(AppContext.BaseDirectory);

    /// <summary>
    /// Resolves the active configuration from the process environment or a test output directory path.
    /// </summary>
    /// <param name="baseDirectory">The base directory to inspect when no explicit environment override exists.</param>
    /// <returns>The resolved configuration name.</returns>
    internal static string ResolveConfiguration(string baseDirectory)
    {
        var explicitConfiguration = Environment.GetEnvironmentVariable(TestConfigurationEnvironmentVariable);
        if (string.Equals(explicitConfiguration, "Release", StringComparison.OrdinalIgnoreCase))
        {
            return "Release";
        }

        if (string.Equals(explicitConfiguration, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            return "Debug";
        }

        for (var directory = new DirectoryInfo(baseDirectory); directory is not null; directory = directory.Parent)
        {
            if (string.Equals(directory.Name, "Release", StringComparison.OrdinalIgnoreCase))
            {
                return "Release";
            }

            if (string.Equals(directory.Name, "Debug", StringComparison.OrdinalIgnoreCase))
            {
                return "Debug";
            }
        }

        return "Debug";
    }
}
