namespace DearStory.Runner.Watching;

/// <summary>
/// Maps changed workspace paths to the host identifiers that require restart.
/// </summary>
public sealed class RestartPlanner
{
    /// <summary>
    /// Plans the host restarts implied by the supplied changed paths.
    /// </summary>
    /// <param name="changedPaths">The changed workspace paths.</param>
    /// <returns>The host identifiers that should rebuild and restart.</returns>
    public IReadOnlyList<string> PlanChanges(IReadOnlyList<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(changedPaths);

        return changedPaths.Any(static path => path.Contains("\\cpp\\", StringComparison.OrdinalIgnoreCase))
            ? ["cpp-host"]
            : changedPaths.Any(static path => path.Contains("\\dotnet\\", StringComparison.OrdinalIgnoreCase))
                ? ["dotnet-host"]
                : [];
    }
}
