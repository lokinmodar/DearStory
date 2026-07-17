using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace DearStory.Capture;

/// <summary>
/// Coordinates frame capture, actual PNG writing, comparison, and manifest emission.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VisualCaptureService
{
    /// <summary>
    /// Executes one visual capture request and returns the ordered story results.
    /// </summary>
    /// <param name="request">The visual capture request to execute.</param>
    /// <param name="frameSource">The frame source used to capture story images.</param>
    /// <param name="cancellationToken">A cancellation token that cancels the capture operation.</param>
    /// <returns>The ordered visual capture results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request" /> or <paramref name="frameSource" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">A captured frame does not contain enough RGBA bytes for the declared dimensions and stride.</exception>
    public async Task<IReadOnlyList<VisualCaptureResult>> ExecuteAsync(
        VisualCaptureRequest request,
        IVisualFrameSource frameSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(frameSource);

        var results = new List<VisualCaptureResult>(request.StoryIds.Count);
        var manifestEntriesByPath = new Dictionary<string, List<ManifestEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var storyId in request.StoryIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frame = await frameSource.CaptureAsync(storyId, request.Backend, cancellationToken).ConfigureAwait(false);
            var artifactPaths = CaptureArtifactLayout.Resolve(
                request.WorkspaceRoot,
                frame.StoryId,
                frame.HostId,
                request.ArtifactRootOverride);

            WriteActualPng(artifactPaths.ActualImagePath, frame);

            if (request.ApproveCanonical)
            {
                CaptureApprovalService.PromoteActualToBaseline(
                    artifactPaths.ActualImagePath,
                    artifactPaths.BaselineImagePath,
                    request.Backend);
            }

            var comparison = ImageComparer.Classify(
                artifactPaths.ActualImagePath,
                artifactPaths.BaselineImagePath,
                request.Backend,
                request.ApproveCanonical,
                artifactPaths.DiffImagePath);

            var result = new VisualCaptureResult(
                StoryId: frame.StoryId,
                HostId: frame.HostId,
                Backend: request.Backend,
                Classification: comparison.Classification,
                ActualImagePath: artifactPaths.ActualImagePath,
                BaselineImagePath: artifactPaths.BaselineImagePath,
                DiffImagePath: comparison.DiffImagePath,
                ManifestPath: artifactPaths.ManifestPath);
            results.Add(result);

            if (!manifestEntriesByPath.TryGetValue(artifactPaths.ManifestPath, out var manifestEntries))
            {
                manifestEntries = [];
                manifestEntriesByPath.Add(artifactPaths.ManifestPath, manifestEntries);
            }

            manifestEntries.Add(CreateManifestEntry(frame, result));
        }

        foreach (var pair in manifestEntriesByPath)
        {
            WriteManifest(pair.Key, pair.Value);
        }

        return results;
    }

    private static ManifestEntry CreateManifestEntry(CapturedFrame frame, VisualCaptureResult result)
    {
        return new ManifestEntry(
            StoryId: result.StoryId,
            HostId: result.HostId,
            Backend: ToKebabCase(result.Backend),
            Width: frame.Width,
            Height: frame.Height,
            Stride: frame.Stride,
            PixelFormat: "rgba8",
            TimestampUtc: frame.TimestampUtc,
            Classification: ToKebabCase(result.Classification),
            ActualImagePath: result.ActualImagePath,
            ActualImageSha256: ComputeFileSha256OrNull(result.ActualImagePath),
            BaselineImagePath: result.BaselineImagePath,
            BaselineImageSha256: ComputeFileSha256OrNull(result.BaselineImagePath),
            DiffImagePath: result.DiffImagePath,
            DiffImageSha256: ComputeFileSha256OrNull(result.DiffImagePath));
    }

    private static void WriteManifest(string manifestPath, IReadOnlyList<ManifestEntry> entries)
    {
        var parent = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var document = new ManifestDocument(entries);
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                document,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
    }

    private static void WriteActualPng(string actualImagePath, CapturedFrame frame)
    {
        var parent = Path.GetDirectoryName(actualImagePath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var bytes = frame.RgbaBytes.Span;
        var minimumLength = ((frame.Height - 1) * frame.Stride) + (frame.Width * 4);
        if (bytes.Length < minimumLength)
        {
            throw new InvalidOperationException("The captured frame does not contain enough RGBA bytes for the declared dimensions.");
        }

        using var bitmap = new Bitmap(frame.Width, frame.Height);
        for (var y = 0; y < frame.Height; y++)
        {
            var rowStart = y * frame.Stride;
            for (var x = 0; x < frame.Width; x++)
            {
                var pixelStart = rowStart + (x * 4);
                bitmap.SetPixel(
                    x,
                    y,
                    Color.FromArgb(
                        bytes[pixelStart + 3],
                        bytes[pixelStart],
                        bytes[pixelStart + 1],
                        bytes[pixelStart + 2]));
            }
        }

        bitmap.Save(actualImagePath, ImageFormat.Png);
    }

    private static string? ComputeFileSha256OrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var hash = SHA256.HashData(File.ReadAllBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToKebabCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (index > 0 && char.IsUpper(character))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private sealed record ManifestDocument(IReadOnlyList<ManifestEntry> Entries);

    private sealed record ManifestEntry(
        string StoryId,
        string HostId,
        string Backend,
        int Width,
        int Height,
        int Stride,
        string PixelFormat,
        DateTimeOffset TimestampUtc,
        string Classification,
        string ActualImagePath,
        string? ActualImageSha256,
        string? BaselineImagePath,
        string? BaselineImageSha256,
        string? DiffImagePath,
        string? DiffImageSha256);
}
