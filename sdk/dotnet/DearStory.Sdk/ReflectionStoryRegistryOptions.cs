namespace DearStory.Sdk;

/// <summary>
/// Configures the DearStory reflection registry fallback.
/// </summary>
public sealed class ReflectionStoryRegistryOptions
{
    /// <summary>
    /// Gets or sets a value that indicates whether runtime reflection fallback is allowed.
    /// </summary>
    /// <value><see langword="true" /> if reflection fallback is allowed; otherwise, <see langword="false" />.</value>
    public bool AllowReflectionFallback { get; init; }
}
