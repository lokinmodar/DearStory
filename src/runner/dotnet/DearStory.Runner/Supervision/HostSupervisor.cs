using DearStory.Runner.Diagnostics;

namespace DearStory.Runner.Supervision;

/// <summary>Supervises host launch attempts with a bounded restart policy.</summary>
public sealed class HostSupervisor
{
    /// <summary>Initializes a new instance of the <see cref="HostSupervisor" /> class.</summary>
    /// <param name="maxRestartAttempts">The maximum number of launch attempts allowed before faulting.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRestartAttempts" /> is not positive.</exception>
    public HostSupervisor(int maxRestartAttempts = 3)
    {
        if (maxRestartAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRestartAttempts), "The maximum restart attempt count must be positive.");
        }

        MaxRestartAttempts = maxRestartAttempts;
    }

    /// <summary>Gets the maximum number of launch attempts allowed before faulting.</summary>
    /// <value>The maximum number of launch attempts allowed before faulting.</value>
    public int MaxRestartAttempts { get; }

    /// <summary>Starts supervision for one host descriptor.</summary>
    /// <param name="descriptor">The host launch descriptor to supervise.</param>
    /// <param name="cancellationToken">The cancellation token that stops supervision.</param>
    /// <returns>The terminal supervision result for the descriptor.</returns>
    public Task<HostSupervisionResult> StartAsync(HostLaunchDescriptor descriptor, CancellationToken cancellationToken)
    {
        return RunUntilTerminalAsync(descriptor, cancellationToken);
    }

    /// <summary>Runs host supervision until the descriptor reaches a terminal state.</summary>
    /// <param name="descriptor">The host launch descriptor to supervise.</param>
    /// <param name="cancellationToken">The cancellation token that stops supervision.</param>
    /// <returns>The terminal supervision result, including the consumed attempt count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor" /> is <see langword="null" />.</exception>
    public async Task<HostSupervisionResult> RunUntilTerminalAsync(HostLaunchDescriptor descriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        StructuredDiagnostic? latestDiagnostic = null;

        for (var attempt = 1; attempt <= MaxRestartAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HostTerminalState state;
            try
            {
                state = await descriptor.LaunchAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new HostSupervisionResult(
                    descriptor.HostId,
                    HostTerminalState.Cancelled,
                    attempt - 1,
                    Array.Empty<StructuredDiagnostic>());
            }

            if (state != HostTerminalState.Faulted)
            {
                return new HostSupervisionResult(
                    descriptor.HostId,
                    state,
                    attempt,
                    latestDiagnostic is null ? Array.Empty<StructuredDiagnostic>() : [latestDiagnostic]);
            }

            latestDiagnostic = new StructuredDiagnostic(
                DateTimeOffset.UtcNow,
                "runner.host.faulted",
                StructuredDiagnosticSeverity.Error,
                $"Host '{descriptor.HostId}' faulted on attempt {attempt}.");
        }

        return new HostSupervisionResult(
            descriptor.HostId,
            HostTerminalState.Faulted,
            MaxRestartAttempts,
            latestDiagnostic is null ? Array.Empty<StructuredDiagnostic>() : [latestDiagnostic]);
    }
}

/// <summary>Represents the terminal state of one supervised host run.</summary>
public enum HostTerminalState
{
    /// <summary>Indicates that the host run completed successfully.</summary>
    Completed,

    /// <summary>Indicates that the host run faulted and exhausted its restart budget.</summary>
    Faulted,

    /// <summary>Indicates that supervision was canceled.</summary>
    Cancelled
}

/// <summary>Represents the terminal result of one supervised host run.</summary>
public sealed class HostSupervisionResult
{
    /// <summary>Initializes a new instance of the <see cref="HostSupervisionResult" /> class.</summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <param name="state">The terminal host state.</param>
    /// <param name="restartAttempts">The number of launch attempts consumed by supervision.</param>
    /// <param name="diagnostics">The structured diagnostics emitted while supervising the host.</param>
    public HostSupervisionResult(
        string hostId,
        HostTerminalState state,
        int restartAttempts,
        IReadOnlyList<StructuredDiagnostic> diagnostics)
    {
        HostId = hostId;
        State = state;
        RestartAttempts = restartAttempts;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the stable host identifier.</summary>
    /// <value>The stable host identifier supervised by the runner.</value>
    public string HostId { get; }

    /// <summary>Gets the terminal host state.</summary>
    /// <value>The terminal state observed by the supervisor.</value>
    public HostTerminalState State { get; }

    /// <summary>Gets the number of launch attempts consumed by supervision.</summary>
    /// <value>The number of launch attempts consumed by supervision, including the terminal attempt.</value>
    public int RestartAttempts { get; }

    /// <summary>Gets the diagnostics emitted while supervising the host.</summary>
    /// <value>The structured diagnostics emitted while supervising the host.</value>
    public IReadOnlyList<StructuredDiagnostic> Diagnostics { get; }
}
