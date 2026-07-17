namespace DearStory.Catalog.Diagnostics;

/// <summary>
/// Maintains the latest host-health snapshot shown in the catalog diagnostics panel.
/// </summary>
public sealed class HostDiagnosticsPanel
{
    private readonly Dictionary<string, HostHealthSnapshot> _hosts = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the ordered host snapshots currently visible to the catalog.
    /// </summary>
    /// <value>The ordered host snapshots currently visible to the catalog.</value>
    public IReadOnlyList<HostHealthSnapshot> Hosts =>
        _hosts.Values.OrderBy(static host => host.HostId, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Records the latest supervision result for one host.
    /// </summary>
    /// <param name="result">The supervision result to surface in the diagnostics panel.</param>
    public void Record(HostHealthSnapshot result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _hosts[result.HostId] = result;
    }
}

/// <summary>
/// Represents the coarse-grained host states surfaced by the catalog diagnostics panel.
/// </summary>
public enum CatalogHostState
{
    /// <summary>
    /// Indicates that the host state has not been observed yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// Indicates that the host completed its latest lifecycle successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Indicates that the host faulted.
    /// </summary>
    Faulted,

    /// <summary>
    /// Indicates that the host was canceled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents one structured diagnostic entry surfaced by the catalog panel.
/// </summary>
/// <param name="TimestampUtc">The UTC timestamp for the diagnostic entry.</param>
/// <param name="Code">The stable diagnostic code.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Message">The human-readable diagnostic message.</param>
public sealed record CatalogHostDiagnostic(
    DateTimeOffset TimestampUtc,
    string Code,
    CatalogDiagnosticSeverity Severity,
    string Message);

/// <summary>
/// Defines the severity levels surfaced by the catalog diagnostics panel.
/// </summary>
public enum CatalogDiagnosticSeverity
{
    /// <summary>
    /// Indicates an informational event.
    /// </summary>
    Info,

    /// <summary>
    /// Indicates a warning event.
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates an error event.
    /// </summary>
    Error
}

/// <summary>
/// Represents one host-health snapshot consumed by the catalog diagnostics panel.
/// </summary>
/// <param name="HostId">The stable host identifier.</param>
/// <param name="State">The latest observed host state.</param>
/// <param name="RestartAttempts">The restart attempts consumed by the host.</param>
/// <param name="Diagnostics">The structured diagnostics associated with the host.</param>
public sealed record HostHealthSnapshot(
    string HostId,
    CatalogHostState State,
    int RestartAttempts,
    IReadOnlyList<CatalogHostDiagnostic> Diagnostics);
