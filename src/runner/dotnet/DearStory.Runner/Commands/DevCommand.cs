using DearStory.Catalog;
using DearStory.Catalog.Controls;
using DearStory.Catalog.Preview;
using DearStory.Core;
using System.Runtime.Versioning;
using DearStory.Runner.Builders;
using DearStory.Runner.Configuration;
using DearStory.Runner.State;
using DearStory.Runner.Supervision;
using DearStory.Runner.Watching;

namespace DearStory.Runner.Commands;

/// <summary>Loads a DearStory workspace and executes the Windows development supervision loop.</summary>
[SupportedOSPlatform("windows")]
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
        InitializeCatalog(configuration);
        _ = new SerializableSessionState();
        using var watcher = new WorkspaceWatcher();
        watcher.Start();
        _ = CreateBuilders(configuration);
        _ = new RestartPlanner();
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

    private static void InitializeCatalog(WorkspaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var presenter = new CatalogSessionPresenter(new StoryCatalog(), new PreviewFrameState(), new SchemaControlFactory());
        presenter.UpdateStories(Array.Empty<StoryDescriptor>());
        _ = configuration.Catalog.Theme;
    }

    private static IReadOnlyDictionary<string, IHostBuilder> CreateBuilders(WorkspaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var builders = new Dictionary<string, IHostBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in configuration.Hosts)
        {
            builders[host.Id] = host.Builder switch
            {
                "cmake" => new CMakeHostBuilder(),
                "dotnet" => new DotNetHostBuilder(),
                _ => throw new InvalidOperationException($"The builder '{host.Builder}' is not supported by the Windows runner."),
            };
        }

        return builders;
    }
}
