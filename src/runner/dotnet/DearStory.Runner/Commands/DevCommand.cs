using DearStory.Runner.Configuration;
using DearStory.Runner.Supervision;

namespace DearStory.Runner.Commands;

/// <summary>Loads a DearStory workspace and executes the Windows development supervision loop.</summary>
public sealed class DevCommand
{
    /// <summary>Executes the <c>dearstory dev</c> command for one workspace.</summary>
    /// <param name="workspacePath">The workspace directory or <c>dearstory.toml</c> file to load.</param>
    /// <param name="cancellationToken">The cancellation token that stops the dev command.</param>
    /// <returns>A stable runner exit code for the attempted dev session.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="workspacePath" /> does not resolve to a valid DearStory workspace.</exception>
    public async Task<RunnerExitCode> ExecuteAsync(string workspacePath, CancellationToken cancellationToken)
    {
        var configuration = WorkspaceConfigurationLoader.Load(workspacePath);
        var supervisor = new HostSupervisor();

        foreach (var host in configuration.Hosts)
        {
            var descriptor = HostLaunchDescriptor.Succeeding(host.Id);
            var result = await supervisor.StartAsync(descriptor, cancellationToken).ConfigureAwait(false);
            if (result.State == HostTerminalState.Faulted)
            {
                return RunnerExitCode.HostLaunchFailure;
            }
        }

        return RunnerExitCode.Success;
    }
}
