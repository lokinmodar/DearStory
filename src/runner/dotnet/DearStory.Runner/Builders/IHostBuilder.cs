namespace DearStory.Runner.Builders;

/// <summary>
/// Builds one DearStory host within the Windows development workflow.
/// </summary>
public interface IHostBuilder
{
    /// <summary>
    /// Executes one host build request.
    /// </summary>
    /// <param name="request">The host build request to execute.</param>
    /// <param name="cancellationToken">The cancellation token that stops the build attempt.</param>
    /// <returns>The result of the attempted build.</returns>
    Task<HostBuildResult> BuildAsync(HostBuildRequest request, CancellationToken cancellationToken);
}
