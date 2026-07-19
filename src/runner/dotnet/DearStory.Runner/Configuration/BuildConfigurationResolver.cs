namespace DearStory.Runner.Configuration;

/// <summary>
/// Resolves the active DearStory build configuration from explicit command input or the current process layout.
/// </summary>
internal static class BuildConfigurationResolver
{
    /// <summary>
    /// Resolves the current Debug or Release configuration.
    /// </summary>
    /// <param name="explicitConfiguration">The optional explicit configuration requested by the caller.</param>
    /// <param name="baseDirectory">The base directory to inspect when no explicit configuration is provided.</param>
    /// <returns>The resolved configuration name.</returns>
    internal static string Resolve(string? explicitConfiguration, string baseDirectory)
    {
        if (string.Equals(explicitConfiguration, "Release", StringComparison.OrdinalIgnoreCase))
        {
            return "Release";
        }

        if (string.Equals(explicitConfiguration, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            return "Debug";
        }

        if (!string.IsNullOrWhiteSpace(explicitConfiguration))
        {
            return explicitConfiguration.Trim();
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
