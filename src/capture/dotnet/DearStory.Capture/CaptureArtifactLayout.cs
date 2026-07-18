using DearStory.Core;

namespace DearStory.Capture;

/// <summary>
/// Resolves canonical baseline and experimental artifact paths for one visual capture result.
/// </summary>
public static class CaptureArtifactLayout
{
    /// <summary>
    /// Resolves the artifact paths for one story capture.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root used to locate the repository root.</param>
    /// <param name="storyId">A raw or canonical story identifier.</param>
    /// <param name="hostId">The host identifier that produced the capture.</param>
    /// <param name="artifactRootOverride">The optional artifact root override path.</param>
    /// <returns>The resolved artifact paths.</returns>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot" />, <paramref name="storyId" />, or <paramref name="hostId" /> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The DearStory repository root could not be resolved from <paramref name="workspaceRoot" />.</exception>
    public static CaptureArtifactPaths Resolve(
        string workspaceRoot,
        string storyId,
        string hostId,
        string? artifactRootOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);

        var repoRoot = ResolveRepositoryRoot(workspaceRoot);
        var canonicalStoryId = StoryId.Parse(storyId).Value;
        var storySegments = canonicalStoryId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var artifactRoot = artifactRootOverride is null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DearStory",
                "visual")
            : Path.GetFullPath(artifactRootOverride);

        var baselineParts = new List<string> { repoRoot, "tests", "visual", "windows", "baselines" };
        baselineParts.AddRange(storySegments[..^1]);
        baselineParts.Add(storySegments[^1] + ".png");

        var actualParts = new List<string> { artifactRoot, "actual", hostId };
        actualParts.AddRange(storySegments);

        var diffParts = new List<string> { artifactRoot, "diff", hostId };
        diffParts.AddRange(storySegments);

        return new CaptureArtifactPaths(
            ActualImagePath: Path.Combine(actualParts.ToArray()) + ".png",
            BaselineImagePath: Path.Combine(baselineParts.ToArray()),
            DiffImagePath: Path.Combine(diffParts.ToArray()) + ".png",
            ManifestPath: Path.Combine(artifactRoot, "capture-results.json"));
    }

    private static string ResolveRepositoryRoot(string workspaceRoot)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(workspaceRoot)); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The DearStory repository root could not be resolved from the workspace root.");
    }
}

/// <summary>
/// Represents the resolved artifact paths for one story capture.
/// </summary>
/// <param name="ActualImagePath">The path to the actual captured image.</param>
/// <param name="BaselineImagePath">The path to the canonical baseline image.</param>
/// <param name="DiffImagePath">The path to the generated diff image.</param>
/// <param name="ManifestPath">The path to the capture manifest file.</param>
public sealed record CaptureArtifactPaths(
    string ActualImagePath,
    string BaselineImagePath,
    string DiffImagePath,
    string ManifestPath);
