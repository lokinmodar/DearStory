namespace DearStory.Runner.Builders;

/// <summary>
/// Describes one host build request issued by the Windows development workflow.
/// </summary>
public sealed class HostBuildRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostBuildRequest" /> class.
    /// </summary>
    /// <param name="hostId">The stable host identifier.</param>
    /// <param name="builderId">The builder kind to use, such as <c>cmake</c> or <c>dotnet</c>.</param>
    /// <param name="project">The workspace project selector associated with the host.</param>
    /// <param name="configuration">The build configuration, such as <c>Release</c>.</param>
    /// <param name="workspaceRootPath">The absolute workspace root path.</param>
    public HostBuildRequest(string hostId, string builderId, string project, string configuration, string workspaceRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentException.ThrowIfNullOrWhiteSpace(builderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);

        HostId = hostId;
        BuilderId = builderId;
        Project = project;
        Configuration = configuration;
        WorkspaceRootPath = workspaceRootPath;
    }

    /// <summary>
    /// Gets the stable host identifier.
    /// </summary>
    /// <value>The stable host identifier.</value>
    public string HostId { get; }

    /// <summary>
    /// Gets the builder kind to execute.
    /// </summary>
    /// <value>The builder kind to execute.</value>
    public string BuilderId { get; }

    /// <summary>
    /// Gets the workspace project selector associated with the host.
    /// </summary>
    /// <value>The workspace project selector associated with the host.</value>
    public string Project { get; }

    /// <summary>
    /// Gets the build configuration.
    /// </summary>
    /// <value>The build configuration.</value>
    public string Configuration { get; }

    /// <summary>
    /// Gets the absolute workspace root path.
    /// </summary>
    /// <value>The absolute workspace root path.</value>
    public string WorkspaceRootPath { get; }
}
