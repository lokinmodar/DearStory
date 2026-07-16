using System.IO;

namespace DearStory.Protocol.E2ETests;

/// <summary>
/// Resolves protocol probe artifact paths for the active test configuration.
/// </summary>
internal static class ProbeArtifacts
{
    private const string TestConfigurationEnvironmentVariable = "DEARSTORY_TEST_CONFIGURATION";

    /// <summary>
    /// Gets the active build configuration used by protocol end-to-end tests.
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

    /// <summary>
    /// Resolves the native protocol probe executable path.
    /// </summary>
    /// <returns>The absolute path to the native probe executable.</returns>
    internal static string ResolveNativeProbe() =>
        Path.Combine(RepositoryRoot.Find(), "artifacts", "bin", "native", CurrentConfiguration(), "dearstory-protocol-probe-cpp.exe");

    /// <summary>
    /// Resolves the managed protocol probe executable path.
    /// </summary>
    /// <returns>The absolute path to the managed probe executable.</returns>
    internal static string ResolveManagedProbe() =>
        Path.Combine(
            RepositoryRoot.Find(),
            "tools",
            "DearStory.ProtocolProbe.DotNet",
            "bin",
            CurrentConfiguration(),
            "net10.0",
            "DearStory.ProtocolProbe.DotNet.exe");
}
