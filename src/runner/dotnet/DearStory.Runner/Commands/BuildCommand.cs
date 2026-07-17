using DearStory.Core;
using DearStory.Docs.StaticHtml;
using DearStory.Runner.Configuration;
using CaptureWorkerProgram = DearStory.CaptureWorker.Program;

namespace DearStory.Runner.Commands;

/// <summary>Loads a DearStory workspace and prepares the static build pipeline entrypoint.</summary>
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
        _ = ParseOptions(arguments);

        var docsDirectory = ResolveDocsDirectory(configuration);
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "docs");
        Directory.CreateDirectory(outputDirectory);

        var stories = new[]
        {
            StoryDescriptor.Create("buttons/primary", "Buttons/Primary"),
            StoryDescriptor.Create("buttons/primarymanaged", "Buttons/PrimaryManaged"),
        };

        var builder = new StaticSiteBuilder();
        await builder.BuildAsync(new BuildRequest(docsDirectory, outputDirectory, stories), cancellationToken).ConfigureAwait(false);

        CaptureWorkerProgram.WriteDeterministicPng(Path.Combine(outputDirectory, "buttons-primary.png"));
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
        var configuration = "Debug";
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], "--configuration", StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new InvalidOperationException("The --configuration option requires a value.");
            }

            configuration = arguments[++index];
        }

        return new BuildCommandOptions(configuration);
    }

    private sealed record BuildCommandOptions(string Configuration);
}
