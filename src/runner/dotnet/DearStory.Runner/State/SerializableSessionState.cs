using System.Text.Json.Nodes;

namespace DearStory.Runner.State;

/// <summary>
/// Persists the serializable story-selection and argument state across host restarts.
/// </summary>
public sealed class SerializableSessionState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SerializableSessionState" /> class.
    /// </summary>
    public SerializableSessionState()
    {
        Arguments = [];
    }

    /// <summary>
    /// Gets the selected story identifier.
    /// </summary>
    /// <value>The selected story identifier, or <see langword="null" /> when no story is selected.</value>
    public string? SelectedStoryId { get; private set; }

    /// <summary>
    /// Gets the current serializable argument snapshot.
    /// </summary>
    /// <value>The current serializable argument snapshot.</value>
    public JsonObject Arguments { get; private set; }

    /// <summary>
    /// Selects the active story identifier.
    /// </summary>
    /// <param name="storyId">The canonical story identifier to persist.</param>
    public void SelectStory(string storyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        SelectedStoryId = storyId;
    }

    /// <summary>
    /// Applies one string argument update.
    /// </summary>
    /// <param name="path">The dotted argument path to update.</param>
    /// <param name="value">The string value to persist.</param>
    public void ApplyString(string path, string value)
    {
        ApplyValue(path, JsonValue.Create(value));
    }

    /// <summary>
    /// Applies one JSON argument update.
    /// </summary>
    /// <param name="path">The dotted argument path to update.</param>
    /// <param name="value">The JSON value to persist.</param>
    public void ApplyValue(string path, JsonNode? value)
    {
        var segments = SplitPath(path);
        JsonObject cursor = Arguments;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (cursor[segments[index]] is not JsonObject child)
            {
                child = [];
                cursor[segments[index]] = child;
            }

            cursor = child;
        }

        cursor[segments[^1]] = value?.DeepClone();
    }

    /// <summary>
    /// Reads one string argument from the current snapshot.
    /// </summary>
    /// <param name="path">The dotted argument path to read.</param>
    /// <returns>The string value at the requested path, or <see langword="null" /> when it is missing.</returns>
    public string? ReadString(string path)
    {
        var segments = SplitPath(path);
        JsonNode? cursor = Arguments;

        foreach (var segment in segments)
        {
            if (cursor is not JsonObject objectCursor || !objectCursor.TryGetPropertyValue(segment, out cursor))
            {
                return null;
            }
        }

        return cursor?.GetValue<string>();
    }

    /// <summary>
    /// Creates a detached snapshot of the current session state.
    /// </summary>
    /// <returns>A detached snapshot of the current session state.</returns>
    public SerializableSessionState Snapshot() =>
        new()
        {
            SelectedStoryId = SelectedStoryId,
            Arguments = (JsonObject)Arguments.DeepClone(),
        };

    /// <summary>
    /// Restores the current state from a detached snapshot.
    /// </summary>
    /// <param name="snapshot">The detached snapshot to restore.</param>
    public void Restore(SerializableSessionState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        SelectedStoryId = snapshot.SelectedStoryId;
        Arguments = (JsonObject)snapshot.Arguments.DeepClone();
    }

    private static string[] SplitPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return path
            .Trim()
            .TrimStart('$')
            .TrimStart('.')
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
