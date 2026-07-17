namespace DearStory.Runner.Builders;

/// <summary>
/// Represents the result of one host build attempt.
/// </summary>
public sealed record HostBuildResult
{
    /// <summary>
    /// Gets the stable host identifier.
    /// </summary>
    /// <value>The stable host identifier.</value>
    public required string HostId { get; init; }

    /// <summary>
    /// Gets the builder kind that produced the result.
    /// </summary>
    /// <value>The builder kind that produced the result.</value>
    public required string BuilderId { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the build succeeded.
    /// </summary>
    /// <value><see langword="true" /> if the build succeeded; otherwise, <see langword="false" />.</value>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the process exit code associated with the build result.
    /// </summary>
    /// <value>The process exit code associated with the build result.</value>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Creates a successful host build result.
    /// </summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <param name="builderId">The builder kind that completed successfully.</param>
    /// <returns>A successful host build result.</returns>
    public static HostBuildResult Success(string hostId, string builderId) =>
        new()
        {
            HostId = hostId,
            BuilderId = builderId,
            Succeeded = true,
            ExitCode = 0,
        };

    /// <summary>
    /// Creates a failed host build result.
    /// </summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <param name="builderId">The builder kind that failed.</param>
    /// <param name="exitCode">The failing process exit code.</param>
    /// <returns>A failed host build result.</returns>
    public static HostBuildResult Failure(string hostId, string builderId, int exitCode) =>
        new()
        {
            HostId = hostId,
            BuilderId = builderId,
            Succeeded = false,
            ExitCode = exitCode,
        };
}
