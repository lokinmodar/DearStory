namespace DearStory.Capture;

/// <summary>
/// Defines the backend used to capture a visual frame.
/// </summary>
public enum CaptureBackendKind
{
    /// <summary>
    /// Uses the software WARP backend.
    /// </summary>
    Warp = 0,

    /// <summary>
    /// Uses the GPU-backed renderer.
    /// </summary>
    Gpu = 1,
}
