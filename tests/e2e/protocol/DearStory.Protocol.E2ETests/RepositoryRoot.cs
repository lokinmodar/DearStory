using System.IO;

namespace DearStory.Protocol.E2ETests;

/// <summary>
/// Resolves the DearStory repository root from the current test output directory.
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>
    /// Finds the repository root that contains the <c>DearStory.slnx</c> file.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    /// <exception cref="InvalidOperationException">
    /// The repository root could not be found by walking upward from <see cref="AppContext.BaseDirectory" />.
    /// </exception>
    internal static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the DearStory repository root.");
    }
}
