namespace DearStory.Capture;

/// <summary>
/// Defines the stable classification for a visual comparison result.
/// </summary>
public enum ComparisonClassification
{
    /// <summary>
    /// Indicates that the actual image matches the baseline.
    /// </summary>
    Match = 0,

    /// <summary>
    /// Indicates that the actual image differs from the baseline.
    /// </summary>
    Mismatch = 1,

    /// <summary>
    /// Indicates that no baseline image was available.
    /// </summary>
    MissingBaseline = 2,

    /// <summary>
    /// Indicates that the result cannot be approved because the backend differs from the canonical policy.
    /// </summary>
    BackendMismatch = 3,

    /// <summary>
    /// Indicates that image capture failed before comparison could complete.
    /// </summary>
    CaptureFault = 4,
}
