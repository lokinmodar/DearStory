namespace DearStory.Core;

/// <summary>
/// Represents a canonical, language-neutral DearStory story identifier.
/// </summary>
/// <param name="Value">The canonical story identifier value.</param>
public readonly record struct StoryId(string Value)
{
    /// <summary>
    /// Parses and canonicalizes a raw story identifier.
    /// </summary>
    /// <param name="raw">A raw story identifier.</param>
    /// <returns>A canonical story identifier.</returns>
    /// <exception cref="ArgumentException"><paramref name="raw" /> is empty or whitespace.</exception>
    public static StoryId Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var normalized = raw.Trim().Replace('\\', '/').ToLowerInvariant();
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("The story identifier must contain at least one non-empty segment.", nameof(raw));
        }

        return new StoryId(string.Join('/', segments));
    }

    /// <summary>
    /// Returns the canonical story identifier value.
    /// </summary>
    /// <returns>The canonical identifier text.</returns>
    public override string ToString() => Value;
}
