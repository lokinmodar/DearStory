using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using DearStory.Core;
using DearStory.Core.Sessions;
using DearStory.Protocol;
using DearStory.Protocol.Generated;
using DearStory.Sdk;
using DearStory.Transport.Windows;
using GeneratedProtocolVersion = DearStory.Protocol.Generated.ProtocolVersion;
using GeneratedStoryDescriptor = DearStory.Protocol.Generated.StoryDescriptor;
using HostProtocolVersion = DearStory.Protocol.ProtocolVersion;

namespace DearStory.Host;

/// <summary>
/// Runs the Windows-first managed DearStory host baseline for official .NET stories.
/// </summary>
/// <remarks>
/// The baseline host validates handshake, story publication, session opening, and RGBA frame transport
/// while keeping rendering deterministic and backend-free. Full ImGui.NET rasterization remains a later slice.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ManagedHost
{
    private const string SchemaDialect = "https://json-schema.org/draft/2020-12/schema";
    private const int FrameWidth = 320;
    private const int FrameHeight = 180;
    private const int FrameStride = FrameWidth * 4;
    private const int FrameSlotCount = 3;

    private readonly string _pipeName;
    private readonly string _hostId;
    private readonly GeneratedStoryRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedHost" /> class.
    /// </summary>
    /// <param name="pipeName">The pipe name without the <c>\\.\pipe\</c> prefix.</param>
    /// <param name="hostId">The stable host identifier echoed into story-index payloads.</param>
    /// <param name="storyAssembly">The loaded story assembly inspected through reflection fallback.</param>
    public ManagedHost(string pipeName, string hostId, Assembly storyAssembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentNullException.ThrowIfNull(storyAssembly);

        _pipeName = pipeName;
        _hostId = hostId;
        _registry = ReflectionStoryRegistry.Create(
            storyAssembly,
            new ReflectionStoryRegistryOptions
            {
                AllowReflectionFallback = true,
            });
    }

    /// <summary>
    /// Connects to the control pipe, publishes the story index, and serves one managed session loop.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var connection = await NamedPipeControlClient.ConnectAsync(_pipeName, cancellationToken).ConfigureAwait(false);
        var negotiatedVersion = await NegotiateAsync(connection, cancellationToken).ConfigureAwait(false);
        await PublishStoryIndexAsync(connection, negotiatedVersion, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var payload = await connection.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                return;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            if (!string.Equals(type, "story_session_open", StringComparison.Ordinal))
            {
                continue;
            }

            var messageId = Guid.Parse(root.GetProperty("messageId").GetString()!);
            var request = JsonSerializer.Deserialize<StorySessionOpen>(root.GetProperty("payload").GetRawText())
                ?? throw new InvalidOperationException("The story session open payload could not be deserialized.");

            var storyRegistration = _registry.Registrations
                .FirstOrDefault(registration => string.Equals(registration.Descriptor.Id.Value, request.StoryId, StringComparison.Ordinal));

            if (storyRegistration is null)
            {
                throw new InvalidOperationException($"The requested managed story '{request.StoryId}' is not registered.");
            }

            var session = StorySession.Open(
                request.SessionId,
                StoryId.Parse(request.StoryId),
                request.InitialArguments.DeepClone(),
                long.Parse(request.RandomSeed, System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.UtcNow);

            await PublishEnvelopeAsync(
                connection,
                negotiatedVersion,
                "story_session_opened",
                new StorySessionOpened
                {
                    ActiveArguments = session.Arguments.DeepClone(),
                    RandomSeed = request.RandomSeed,
                    SessionId = request.SessionId,
                    StartTimeUtc = request.StartTimeUtc,
                    StoryId = request.StoryId,
                },
                cancellationToken,
                correlationId: messageId,
                sessionId: request.SessionId).ConfigureAwait(false);

            var descriptor = FrameTransportDescriptor.Create(
                $"Local\\dearstory-managed-frame-{request.SessionId:D}",
                FrameWidth,
                FrameHeight,
                FrameStride,
                FrameSlotCount);

            using var writer = new SharedMemoryFrameWriter(descriptor);

            await PublishEnvelopeAsync(
                connection,
                negotiatedVersion,
                "frame_channel_ready",
                new FrameChannelReady
                {
                    ColorSpace = "srgb",
                    Height = FrameHeight,
                    MappingName = descriptor.MappingName,
                    PixelFormat = "rgba8",
                    SessionId = request.SessionId,
                    SlotCount = FrameSlotCount,
                    Stride = FrameStride,
                    Width = FrameWidth,
                },
                cancellationToken,
                sessionId: request.SessionId).ConfigureAwait(false);

            var frame = writer.Publish(CreateDeterministicFrame());

            await PublishEnvelopeAsync(
                connection,
                negotiatedVersion,
                "frame_presented",
                new FramePresented
                {
                    Sequence = frame.Sequence,
                    SessionId = request.SessionId,
                    SlotIndex = frame.SlotIndex,
                    TimestampUtc = DateTimeOffset.UtcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
                },
                cancellationToken,
                sessionId: request.SessionId).ConfigureAwait(false);

            while (await connection.ReadAsync(cancellationToken).ConfigureAwait(false) is not null)
            {
            }

            return;
        }
    }

    private static byte[] CreateDeterministicFrame()
    {
        var pixels = new byte[FrameHeight * FrameStride];

        for (var y = 0; y < FrameHeight; y++)
        {
            for (var x = 0; x < FrameWidth; x++)
            {
                WritePixel(pixels, x, y, 24, 26, 31, 255);
            }
        }

        for (var y = 48; y < FrameHeight - 48; y++)
        {
            for (var x = 64; x < FrameWidth - 64; x++)
            {
                var onBorder = x == 64 || x == FrameWidth - 65 || y == 48 || y == FrameHeight - 49;
                if (onBorder)
                {
                    WritePixel(pixels, x, y, 217, 226, 236, 255);
                }
                else
                {
                    WritePixel(pixels, x, y, 72, 136, 255, 255);
                }
            }
        }

        return pixels;
    }

    private static void WritePixel(byte[] pixels, int x, int y, byte red, byte green, byte blue, byte alpha)
    {
        var offset = (y * FrameStride) + (x * 4);
        pixels[offset + 0] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
        pixels[offset + 3] = alpha;
    }

    private async Task<HostProtocolVersion> NegotiateAsync(NamedPipeConnection connection, CancellationToken cancellationToken)
    {
        var hello = new ControlEnvelope(
            new HostProtocolVersion(HostProtocolVersion.CurrentMajor, HostProtocolVersion.CurrentMinor),
            "hello",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Hello
            {
                Implementation = new ImplementationIdentity
                {
                    Binding = "ImGui.NET",
                    DearImGuiIdentity = "ImGui.NET@1.91.6.1",
                    DearImGuiVersion = "1.91.6.1",
                    Language = "csharp",
                    Name = "DearStory.Host",
                    Toolchain = $".NET {Environment.Version}",
                    Version = "0.1.0",
                },
                RequiredCapabilities = [],
                Role = PeerRole.Host,
                SupportedCapabilities = ["control.handshake.v1", "story.run"],
            });

        await connection.WriteAsync(ControlCodec.Encode(hello), cancellationToken).ConfigureAwait(false);

        var responsePayload = await connection.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The runner disconnected before completing the managed host handshake.");

        var result = ControlCodec.Decode(responsePayload);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        if (!string.Equals(result.Value!.Type, "welcome", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The managed host expected a welcome envelope from the runner.");
        }

        return result.Value.Protocol;
    }

    private async Task PublishStoryIndexAsync(NamedPipeConnection connection, HostProtocolVersion version, CancellationToken cancellationToken)
    {
        var payload = new StoryIndexPublished
        {
            HostId = _hostId,
            Stories = _registry.Registrations.Select(CreateWireDescriptor).ToArray(),
        };

        await PublishEnvelopeAsync(connection, version, "story_index_published", payload, cancellationToken).ConfigureAwait(false);
    }

    private static GeneratedStoryDescriptor CreateWireDescriptor(GeneratedStoryRegistration registration) =>
        new()
        {
            ArgumentSchema = new StoryArgumentSchema
            {
                Dialect = SchemaDialect,
                Schema = registration.ArgumentSchema.Document,
            },
            Capabilities = registration.Descriptor.Capabilities.ToArray(),
            DefaultArguments = registration.DefaultArguments.DeepClone(),
            Description = registration.Descriptor.Description,
            Hierarchy = registration.Descriptor.Hierarchy.ToArray(),
            Id = registration.Descriptor.Id.Value,
            SourcePath = registration.Descriptor.SourcePath,
            Tags = registration.Descriptor.Tags.ToArray(),
            Title = registration.Descriptor.Title,
        };

    private static async Task PublishEnvelopeAsync<TPayload>(
        NamedPipeConnection connection,
        HostProtocolVersion version,
        string type,
        TPayload payload,
        CancellationToken cancellationToken,
        Guid? correlationId = null,
        Guid? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(payload);

        var envelope = new JsonObject
        {
            ["protocol"] = new JsonObject
            {
                ["major"] = version.Major,
                ["minor"] = version.Minor,
            },
            ["type"] = type,
            ["messageId"] = Guid.NewGuid().ToString("D"),
            ["timestamp"] = DateTimeOffset.UtcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["payload"] = JsonSerializer.SerializeToNode(payload)
                ?? throw new InvalidOperationException("The managed host control payload could not be serialized."),
        };

        if (correlationId.HasValue)
        {
            envelope["correlationId"] = correlationId.Value.ToString("D");
        }

        if (sessionId.HasValue)
        {
            envelope["sessionId"] = sessionId.Value.ToString("D");
        }

        await connection.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(envelope), cancellationToken).ConfigureAwait(false);
    }
}
