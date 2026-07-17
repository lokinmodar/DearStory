using DearStory.Runner.Builders;
using DearStory.Runner.Configuration;
using DearStory.Runner.Diagnostics;
using DearStory.Runner.State;
using DearStory.Runner.Supervision;
using DearStory.Runner.Watching;

namespace DearStory.WindowsSlice.Tests;

internal sealed class WindowsSliceHarness : IAsyncDisposable
{
    private readonly WorkspaceConfiguration _configuration;
    private readonly WorkspaceWatcher _watcher;
    private readonly RestartPlanner _planner;
    private readonly SerializableSessionState _sessionState;
    private readonly Dictionary<string, RecordingHostBuilder> _builders;
    private readonly List<HostRestartRecord> _restarts = [];
    private readonly List<HostFaultRecord> _faults = [];
    private readonly Dictionary<string, int> _remainingFaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _restartCounts = new(StringComparer.OrdinalIgnoreCase);

    private WindowsSliceHarness(WorkspaceConfiguration configuration)
    {
        _configuration = configuration;
        _planner = new RestartPlanner();
        _watcher = new WorkspaceWatcher(HandleChangesAsync);
        _sessionState = new SerializableSessionState();
        _builders = configuration.Hosts.ToDictionary(
            static host => host.Id,
            static host => new RecordingHostBuilder(host.Id),
            StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<WindowsSliceHarness> StartAsync()
    {
        var workspaceFile = Path.Combine(ResolveRepositoryRoot(), "examples", "workspaces", "windows-slice", "dearstory.toml");
        var configuration = WorkspaceConfigurationLoader.Load(workspaceFile);
        var harness = new WindowsSliceHarness(configuration);
        harness._watcher.Start();
        await Task.CompletedTask;
        return harness;
    }

    public Task SelectStoryAsync(string storyId)
    {
        _sessionState.SelectStory(storyId);
        return Task.CompletedTask;
    }

    public Task ApplyArgumentAsync(string path, string value)
    {
        _sessionState.ApplyString(path, value);
        return Task.CompletedTask;
    }

    public Task TouchCppStoryAsync()
    {
        var changedPath = Path.Combine(_configuration.Workspace.RootPath, "cpp", "src", "buttons_primary.cpp");
        return _watcher.PublishChangesAsync([changedPath], CancellationToken.None);
    }

    public Task<string?> ReadCurrentArgumentAsync(string path) => Task.FromResult(_sessionState.ReadString(path));

    public Task<bool> WasHostRestartedAsync(string hostId) =>
        Task.FromResult(_restartCounts.TryGetValue(hostId, out var count) && count > 0);

    public Task<HostRestartRecord> WaitForHostRestartAsync(string hostId) =>
        Task.FromResult(_restarts.Last(restart => string.Equals(restart.HostId, hostId, StringComparison.OrdinalIgnoreCase)));

    public Task SimulateHostCrashAsync(string hostId)
    {
        _remainingFaults[hostId] = 3;
        var changedPath = Path.Combine(_configuration.Workspace.RootPath, "cpp", "src", "buttons_primary.cpp");
        return _watcher.PublishChangesAsync([changedPath], CancellationToken.None);
    }

    public Task<HostFaultRecord> WaitForHostFaultAsync(string hostId) =>
        Task.FromResult(_faults.Last(fault => string.Equals(fault.HostId, hostId, StringComparison.OrdinalIgnoreCase)));

    public ValueTask DisposeAsync()
    {
        _watcher.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task HandleChangesAsync(IReadOnlyList<string> changedPaths, CancellationToken cancellationToken)
    {
        var affectedHosts = _planner.PlanChanges(changedPaths);
        foreach (var hostId in affectedHosts)
        {
            var hostConfiguration = _configuration.Hosts.Single(host => string.Equals(host.Id, hostId, StringComparison.OrdinalIgnoreCase));
            await _builders[hostId].BuildAsync(
                new HostBuildRequest(hostConfiguration.Id, hostConfiguration.Builder, hostConfiguration.Project ?? string.Empty, "Release", _configuration.Workspace.RootPath),
                cancellationToken).ConfigureAwait(false);

            var supervisor = new HostSupervisor();
            var descriptor = new HostLaunchDescriptor(hostId, _ => ValueTask.FromResult(NextLaunchState(hostId)));
            var result = await supervisor.StartAsync(descriptor, cancellationToken).ConfigureAwait(false);
            if (result.State == HostTerminalState.Faulted)
            {
                _faults.Add(new HostFaultRecord(result.HostId, result.RestartAttempts, result.Diagnostics));
                continue;
            }

            _restartCounts[hostId] = _restartCounts.GetValueOrDefault(hostId) + 1;
            _restarts.Add(new HostRestartRecord(hostId, _sessionState.Snapshot()));
        }
    }

    private HostTerminalState NextLaunchState(string hostId)
    {
        if (_remainingFaults.TryGetValue(hostId, out var remainingFaults) && remainingFaults > 0)
        {
            _remainingFaults[hostId] = remainingFaults - 1;
            return HostTerminalState.Faulted;
        }

        return HostTerminalState.Completed;
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "examples", "workspaces", "windows-slice", "dearstory.toml");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The DearStory repository root could not be resolved from the integration test base directory.");
    }

    private sealed class RecordingHostBuilder(string hostId) : IHostBuilder
    {
        public string HostId { get; } = hostId;

        public Task<HostBuildResult> BuildAsync(HostBuildRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(HostBuildResult.Success(request.HostId, request.BuilderId));
        }
    }
}

internal sealed record HostRestartRecord(string HostId, SerializableSessionState RestoredState);

internal sealed record HostFaultRecord(string HostId, int RestartAttempts, IReadOnlyList<StructuredDiagnostic> Diagnostics);
