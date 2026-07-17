namespace DearStory.Runner.Supervision;

/// <summary>Describes one host launch operation supervised by the Windows runner.</summary>
public sealed class HostLaunchDescriptor
{
    private readonly Func<CancellationToken, ValueTask<HostTerminalState>> _launchAsync;

    /// <summary>Initializes a new instance of the <see cref="HostLaunchDescriptor" /> class.</summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <param name="launchAsync">The asynchronous launch delegate to supervise.</param>
    /// <exception cref="ArgumentException"><paramref name="hostId" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="launchAsync" /> is <see langword="null" />.</exception>
    public HostLaunchDescriptor(string hostId, Func<CancellationToken, ValueTask<HostTerminalState>> launchAsync)
    {
        if (string.IsNullOrWhiteSpace(hostId))
        {
            throw new ArgumentException("A host identifier must be provided.", nameof(hostId));
        }

        ArgumentNullException.ThrowIfNull(launchAsync);

        HostId = hostId;
        _launchAsync = launchAsync;
    }

    /// <summary>Gets the stable host identifier.</summary>
    /// <value>The stable host identifier supervised by the runner.</value>
    public string HostId { get; }

    /// <summary>Creates a descriptor that always faults for retry-policy tests.</summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <returns>A host launch descriptor that terminates in the faulted state.</returns>
    public static HostLaunchDescriptor Failing(string hostId)
    {
        return new HostLaunchDescriptor(hostId, _ => ValueTask.FromResult(HostTerminalState.Faulted));
    }

    /// <summary>Creates a descriptor that completes immediately.</summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <returns>A host launch descriptor that terminates successfully.</returns>
    public static HostLaunchDescriptor Succeeding(string hostId)
    {
        return new HostLaunchDescriptor(hostId, _ => ValueTask.FromResult(HostTerminalState.Completed));
    }

    /// <summary>Executes the launch delegate for one supervision attempt.</summary>
    /// <param name="cancellationToken">The cancellation token that stops the launch attempt.</param>
    /// <returns>The terminal state reported by the launch delegate.</returns>
    public ValueTask<HostTerminalState> LaunchAsync(CancellationToken cancellationToken)
    {
        return _launchAsync(cancellationToken);
    }
}
