using System.Runtime.Versioning;
using DearStory.Capture;
using DearStory.Runner.Configuration;

namespace DearStory.Runner.Capture;

/// <summary>
/// Captures real RGBA frames from configured DearStory hosts through the existing control protocol.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RunnerHostCaptureAdapter : IVisualFrameSource
{
    private readonly WorkspaceConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunnerHostCaptureAdapter" /> class.
    /// </summary>
    /// <param name="configuration">The loaded workspace configuration that defines the available hosts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
    public RunnerHostCaptureAdapter(WorkspaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="storyId" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No configured host publishes the requested story identifier.</exception>
    public async Task<CapturedFrame> CaptureAsync(string storyId, CaptureBackendKind backend, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storyId))
        {
            throw new ArgumentException("A story identifier must be provided.", nameof(storyId));
        }

        var publishedStories = new List<string>();

        foreach (var host in _configuration.Hosts)
        {
            await using var session = await RunnerHostCaptureSession.StartAsync(_configuration, host, cancellationToken).ConfigureAwait(false);
            if (!PublishesStory(session.PublishedStoryIds, storyId))
            {
                foreach (var publishedStoryId in session.PublishedStoryIds)
                {
                    publishedStories.Add($"{session.HostId}:{publishedStoryId}");
                }

                continue;
            }

            return await session.CaptureAsync(storyId, backend, cancellationToken).ConfigureAwait(false);
        }

        var availableStories = publishedStories.Count == 0
            ? "<none>"
            : string.Join(", ", publishedStories.OrderBy(static item => item, StringComparer.Ordinal));

        throw new InvalidOperationException(
            $"The story '{storyId}' is not published by any configured host. Available stories: {availableStories}.");
    }

    private static bool PublishesStory(IReadOnlyList<string> publishedStoryIds, string requestedStoryId)
    {
        foreach (var publishedStoryId in publishedStoryIds)
        {
            if (string.Equals(publishedStoryId, requestedStoryId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
