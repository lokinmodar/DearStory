using DearStory.Runner.Configuration;

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
        cancellationToken.ThrowIfCancellationRequested();
        _ = WorkspaceConfigurationLoader.Load(workspacePath);
        return Task.FromResult(RunnerExitCode.Success);
    }
}
