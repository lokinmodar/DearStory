using DearStory.Capture;
using DearStory.Core;
using DearStory.Docs.StaticHtml;
using DearStory.Runner.Capture;
using DearStory.Runner.Configuration;
using System.Runtime.Versioning;

namespace DearStory.Runner.Commands;

/// <summary>Loads a DearStory workspace and prepares the static build pipeline entrypoint.</summary>
[SupportedOSPlatform("windows")]
public sealed class BuildCommand
{
    /// <summary>Executes the <c>dearstory build</c> command for one workspace.</summary>
    /// <param name="workspacePath">The workspace directory or <c>dearstory.toml</c> file to load.</param>
    /// <param name="cancellationToken">The cancellation token that stops the build command.</param>
    /// <returns>A stable runner exit code for the attempted build operation.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="workspacePath" /> does not resolve to a valid DearStory workspace.</exception>
    public Task<RunnerExitCode> ExecuteAsync(string workspacePath, CancellationToken cancellationToken)
    {
        return ExecuteAsync(workspacePath, Array.Empty<string>(), cancellationToken);
    }

    /// <summary>Executes the <c>dearstory build</c> command for one workspace.</summary>
    /// <param name="workspacePath">The workspace directory or <c>dearstory.toml</c> file to load.</param>
    /// <param name="arguments">The optional command arguments that refine the build behavior.</param>
    /// <param name="cancellationToken">The cancellation token that stops the build command.</param>
    /// <returns>A stable runner exit code for the attempted build operation.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="workspacePath" /> does not resolve to a valid DearStory workspace.</exception>
    public async Task<RunnerExitCode> ExecuteAsync(string workspacePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = WorkspaceConfigurationLoader.Load(workspacePath);
        var options = ParseOptions(arguments);

        if (options.Approve && options.VisualBackend != CaptureBackendKind.Warp)
        {
            throw new InvalidOperationException("Canonical approval requires the WARP backend.");
        }

        var docsDirectory = ResolveDocsDirectory(configuration);
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "docs");
        Directory.CreateDirectory(outputDirectory);
        var storyIds = ResolveStoryIds(configuration, options);

        var frameSource = new RunnerHostCaptureAdapter(configuration, options.Configuration);
        var captureService = new VisualCaptureService();
        var results = await captureService.ExecuteAsync(
            new VisualCaptureRequest(
                WorkspaceRoot: configuration.Workspace.RootPath,
                StoryIds: storyIds,
                Backend: options.VisualBackend,
                CanonicalOnly: options.CanonicalOnly,
                ApproveCanonical: options.Approve,
                ArtifactRootOverride: Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT")),
            frameSource,
            cancellationToken).ConfigureAwait(false);

        var builder = new StaticSiteBuilder();
        await CopyDocsScreenshotsAsync(results, outputDirectory, cancellationToken).ConfigureAwait(false);
        await builder.BuildAsync(
            new BuildRequest(
                docsDirectory,
                outputDirectory,
                ResolveStories(results)),
            cancellationToken).ConfigureAwait(false);

        return RunnerExitCode.Success;
    }

    private static string ResolveDocsDirectory(WorkspaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var docsGlob = configuration.Docs.FirstOrDefault()?.Glob ?? "docs/**/*.md";
        var separatorIndex = docsGlob.IndexOf("**", StringComparison.Ordinal);
        var relativeDocsDirectory = separatorIndex >= 0 ? docsGlob[..separatorIndex] : Path.GetDirectoryName(docsGlob) ?? docsGlob;
        relativeDocsDirectory = relativeDocsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(configuration.Workspace.RootPath, relativeDocsDirectory));
    }

    private static BuildCommandOptions ParseOptions(IReadOnlyList<string> arguments)
    {
        var configuration = BuildConfigurationResolver.Resolve(null, AppContext.BaseDirectory);
        var visualBackend = CaptureBackendKind.Warp;
        var canonicalOnly = false;
        var approve = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], "--configuration", StringComparison.Ordinal))
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("The --configuration option requires a value.");
                }

                configuration = arguments[++index];
                continue;
            }

            if (string.Equals(arguments[index], "--visual-backend", StringComparison.Ordinal))
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("The --visual-backend option requires a value.");
                }

                visualBackend = ParseVisualBackend(arguments[++index]);
                continue;
            }

            if (string.Equals(arguments[index], "--canonical-only", StringComparison.Ordinal))
            {
                canonicalOnly = true;
                continue;
            }

            if (string.Equals(arguments[index], "--approve", StringComparison.Ordinal))
            {
                approve = true;
            }
        }

        return new BuildCommandOptions(configuration, visualBackend, canonicalOnly, approve);
    }

    private static CaptureBackendKind ParseVisualBackend(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "warp" => CaptureBackendKind.Warp,
            "gpu" => CaptureBackendKind.Gpu,
            _ => throw new InvalidOperationException($"The visual backend '{value}' is not supported. Use 'warp' or 'gpu'."),
        };
    }

    private static IReadOnlyList<string> ResolveStoryIds(WorkspaceConfiguration configuration, BuildCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var configured = configuration.Visual.Overrides
            .Where(static item => item.IncludeInCanonicalCorpus)
            .Select(static item => item.StoryId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        return options.CanonicalOnly || configured.Length > 0
            ? configured
            : ["buttons/primary", "buttons/primarymanaged"];
    }

    private static Task CopyDocsScreenshotsAsync(
        IReadOnlyList<VisualCaptureResult> results,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationFileName = result.StoryId
                .Replace('/', '-')
                .Replace('\\', '-') + ".png";
            File.Copy(result.ActualImagePath, Path.Combine(outputDirectory, destinationFileName), overwrite: true);
        }

        var firstManifest = results.Select(static item => item.ManifestPath).FirstOrDefault();
        if (firstManifest is not null && File.Exists(firstManifest))
        {
            File.Copy(firstManifest, Path.Combine(outputDirectory, "capture-results.json"), overwrite: true);
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<StoryDescriptor> ResolveStories(IReadOnlyList<VisualCaptureResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return results
            .Select(static result => StoryDescriptor.Create(result.StoryId, result.StoryId.Split('/')[^1]))
            .ToArray();
    }

    private sealed record BuildCommandOptions(
        string Configuration,
        CaptureBackendKind VisualBackend,
        bool CanonicalOnly,
        bool Approve);
}
