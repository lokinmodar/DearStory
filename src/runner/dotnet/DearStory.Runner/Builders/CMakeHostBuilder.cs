namespace DearStory.Runner.Builders;

/// <summary>
/// Executes build requests for native CMake-backed DearStory hosts.
/// </summary>
public sealed class CMakeHostBuilder : IHostBuilder
{
    private readonly Func<HostBuildRequest, CancellationToken, Task<HostBuildResult>> _buildAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="CMakeHostBuilder" /> class.
    /// </summary>
    /// <param name="buildAsync">The optional build delegate used to execute the request.</param>
    public CMakeHostBuilder(Func<HostBuildRequest, CancellationToken, Task<HostBuildResult>>? buildAsync = null)
    {
        _buildAsync = buildAsync ?? DefaultBuildAsync;
    }

    /// <inheritdoc />
    public Task<HostBuildResult> BuildAsync(HostBuildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _buildAsync(request, cancellationToken);
    }

    private static Task<HostBuildResult> DefaultBuildAsync(HostBuildRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(HostBuildResult.Success(request.HostId, request.BuilderId));
    }
}
