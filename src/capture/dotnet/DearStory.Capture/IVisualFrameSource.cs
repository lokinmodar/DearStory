namespace DearStory.Capture;

/// <summary>
/// Captures visual frames for DearStory stories.
/// </summary>
public interface IVisualFrameSource
{
    /// <summary>
    /// Captures one RGBA frame for the specified story and backend.
    /// </summary>
    /// <param name="storyId">A canonical or raw story identifier.</param>
    /// <param name="backend">One of the enumeration values that specifies the capture backend.</param>
    /// <param name="cancellationToken">A cancellation token that cancels the capture operation.</param>
    /// <returns>A task that returns the captured frame.</returns>
    Task<CapturedFrame> CaptureAsync(string storyId, CaptureBackendKind backend, CancellationToken cancellationToken);
}
