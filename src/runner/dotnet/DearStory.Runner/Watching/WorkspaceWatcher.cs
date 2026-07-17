namespace DearStory.Runner.Watching;

/// <summary>
/// Coordinates workspace change notifications for the Windows development loop.
/// </summary>
public sealed class WorkspaceWatcher : IDisposable
{
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task>? _onChangedAsync;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceWatcher" /> class.
    /// </summary>
    /// <param name="onChangedAsync">The optional asynchronous callback invoked for published changes.</param>
    public WorkspaceWatcher(Func<IReadOnlyList<string>, CancellationToken, Task>? onChangedAsync = null)
    {
        _onChangedAsync = onChangedAsync;
    }

    /// <summary>
    /// Gets a value that indicates whether the watcher is running.
    /// </summary>
    /// <value><see langword="true" /> when the watcher is running; otherwise, <see langword="false" />.</value>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts the watcher.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IsRunning = true;
    }

    /// <summary>
    /// Stops the watcher.
    /// </summary>
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        IsRunning = false;
    }

    /// <summary>
    /// Publishes one batch of changed paths to the watcher callback.
    /// </summary>
    /// <param name="changedPaths">The changed workspace paths.</param>
    /// <param name="cancellationToken">The cancellation token that stops callback execution.</param>
    /// <returns>A task that completes when the change callback finishes.</returns>
    public Task PublishChangesAsync(IReadOnlyList<string> changedPaths, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(changedPaths);

        if (!IsRunning || _onChangedAsync is null)
        {
            return Task.CompletedTask;
        }

        return _onChangedAsync(changedPaths, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }
}
