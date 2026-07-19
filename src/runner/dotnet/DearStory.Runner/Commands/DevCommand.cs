using DearStory.Capture;
using DearStory.Catalog;
using DearStory.Catalog.Controls;
using DearStory.Catalog.Preview;
using DearStory.Core;
using DearStory.Runner.Capture;
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
        return await ExecuteAsync(workspacePath, Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes the <c>dearstory dev</c> command for one workspace.</summary>
    /// <param name="workspacePath">The workspace directory or <c>dearstory.toml</c> file to load.</param>
    /// <param name="arguments">The optional command arguments that refine the dev behavior.</param>
    /// <param name="cancellationToken">The cancellation token that stops the dev command.</param>
    /// <returns>A stable runner exit code for the attempted dev session.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="workspacePath" /> does not resolve to a valid DearStory workspace.</exception>
    public async Task<RunnerExitCode> ExecuteAsync(string workspacePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var configuration = WorkspaceConfigurationLoader.Load(workspacePath);
        var options = ParseOptions(arguments);

        if (options.Approve && options.VisualBackend != CaptureBackendKind.Warp)
        {
            throw new InvalidOperationException("Canonical approval requires the WARP backend.");
        }

        if (options.CaptureStoryId is not null)
        {
            var frameSource = new RunnerHostCaptureAdapter(configuration, options.Configuration);
            var captureService = new VisualCaptureService();
            var results = await captureService.ExecuteAsync(
                new VisualCaptureRequest(
                    WorkspaceRoot: configuration.Workspace.RootPath,
                    StoryIds: [options.CaptureStoryId],
                    Backend: options.VisualBackend,
                    CanonicalOnly: false,
                    ApproveCanonical: options.Approve,
                    ArtifactRootOverride: Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT")),
                frameSource,
                cancellationToken).ConfigureAwait(false);

            foreach (var result in results)
            {
                Console.WriteLine(result.ManifestPath);
            }

            return RunnerExitCode.Success;
        }

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

    private static DevCommandOptions ParseOptions(IReadOnlyList<string> arguments)
    {
        string? captureStoryId = null;
        var configuration = BuildConfigurationResolver.Resolve(null, AppContext.BaseDirectory);
        var visualBackend = CaptureBackendKind.Warp;
        var approve = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], "--capture-story", StringComparison.Ordinal))
            {
                if (index + 1 >= arguments.Count)
                {
                    throw new InvalidOperationException("The --capture-story option requires a value.");
                }

                captureStoryId = arguments[++index];
                continue;
            }

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

            if (string.Equals(arguments[index], "--approve", StringComparison.Ordinal))
            {
                approve = true;
            }
        }

        return new DevCommandOptions(captureStoryId, configuration, visualBackend, approve);
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

    private sealed record DevCommandOptions(string? CaptureStoryId, string Configuration, CaptureBackendKind VisualBackend, bool Approve);
}
