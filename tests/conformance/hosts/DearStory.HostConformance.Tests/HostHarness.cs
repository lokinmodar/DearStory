using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DearStory.Protocol;
using DearStory.Protocol.Generated;
using DearStory.Protocol.Windows;
using DearStory.Transport.Windows;
using GeneratedProtocolVersion = DearStory.Protocol.Generated.ProtocolVersion;

namespace DearStory.HostConformance.Tests;

/// <summary>Provides a focused Windows host harness for native and managed host conformance tests.</summary>
[SupportedOSPlatform("windows")]
internal sealed class HostHarness : IAsyncDisposable
{
    private static readonly DearStory.Protocol.ProtocolVersion ProtocolVersion = new(1, 0);

    private readonly NamedPipeControlServer _server;
    private readonly NamedPipeConnection _connection;
    private readonly Process _process;
    private readonly CancellationTokenSource _lifetime;

    private HostHarness(
        string hostId,
        NamedPipeControlServer server,
        NamedPipeConnection connection,
        Process process,
        CancellationTokenSource lifetime)
    {
        HostId = hostId;
        _server = server;
        _connection = connection;
        _process = process;
        _lifetime = lifetime;
    }

    public string HostId { get; }

    public static async Task<HostHarness> StartAsync(string hostId)
    {
        var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pipeName = $"dearstory-host-{Guid.NewGuid():N}";
        var server = new NamedPipeControlServer(pipeName);
        var process = StartHost(pipeName, hostId);

        try
        {
            var connection = await server.AcceptAsync(lifetime.Token).ConfigureAwait(false);
            var helloEnvelope = await ReadHandshakeHelloAsync(connection, lifetime.Token).ConfigureAwait(false);
            await WriteWelcomeAsync(connection, helloEnvelope, lifetime.Token).ConfigureAwait(false);

            return new HostHarness(hostId, server, connection, process, lifetime);
        }
        catch
        {
            lifetime.Dispose();
            await server.DisposeAsync().ConfigureAwait(false);
            process.Kill(entireProcessTree: true);
            process.Dispose();
            throw;
        }
    }

    public async Task<IReadOnlyList<HostStoryDescriptor>> WaitForStoryIndexAsync()
    {
        while (true)
        {
            var envelope = await ReadEnvelopeAsync(_connection, _lifetime.Token).ConfigureAwait(false);
            if (!string.Equals(envelope.Type, "story_index_published", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = envelope.DeserializePayload<StoryIndexPublished>();
            return payload.Stories.Select(
                story => new HostStoryDescriptor(
                    story.Id,
                    story.Title)).ToArray();
        }
    }

    public async Task<HostFrameSnapshot> OpenSessionAndReadFrameAsync(string storyId)
    {
        var sessionId = Guid.NewGuid();
        var observedTypes = new List<string>();
        var openPayload = new StorySessionOpen
        {
            InitialArguments = JsonNode.Parse("{}")!,
            RandomSeed = "1",
            SessionId = sessionId,
            StartTimeUtc = FormatTimestamp(DateTimeOffset.UtcNow),
            StoryId = storyId
        };

        await WriteEnvelopeAsync(
            _connection,
            "story_session_open",
            openPayload,
            sessionId: sessionId,
            cancellationToken: _lifetime.Token).ConfigureAwait(false);

        FrameChannelReady? frameDescriptor = null;

        try
        {
            while (true)
            {
                var envelope = await ReadEnvelopeAsync(_connection, _lifetime.Token).ConfigureAwait(false);
                observedTypes.Add(envelope.Type);
                if (envelope.SessionId != sessionId)
                {
                    continue;
                }

                if (string.Equals(envelope.Type, "frame_channel_ready", StringComparison.Ordinal))
                {
                    frameDescriptor = envelope.DeserializePayload<FrameChannelReady>();
                    continue;
                }

                if (string.Equals(envelope.Type, "frame_presented", StringComparison.Ordinal))
                {
                    if (frameDescriptor is null)
                    {
                        throw new InvalidOperationException("A frame was presented before a frame channel descriptor was published.");
                    }

                    var transportDescriptor = FrameTransportDescriptor.Create(
                        frameDescriptor.MappingName,
                        frameDescriptor.Width,
                        frameDescriptor.Height,
                        frameDescriptor.Stride,
                        frameDescriptor.SlotCount);

                    using var reader = new SharedMemoryFrameReader(transportDescriptor);
                    if (!reader.TryReadLatest(out var frame))
                    {
                        throw new InvalidOperationException("The shared-memory frame channel did not expose a readable frame.");
                    }

                    return new HostFrameSnapshot(frameDescriptor.Width, frameDescriptor.Height, frame.Sequence, frame.Bytes);
                }
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"{exception.Message} ObservedTypes={string.Join(",", observedTypes)}",
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        await _server.DisposeAsync().ConfigureAwait(false);
        _process.Dispose();
        _lifetime.Dispose();
    }

    private static Process StartHost(string pipeName, string hostId) =>
        hostId switch
        {
            "cpp-host" => StartNativeHost(pipeName, hostId),
            "dotnet-host" => StartManagedHost(pipeName, hostId),
            _ => throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "The requested host is not supported by the conformance harness."),
        };

    private static Process StartNativeHost(string pipeName, string hostId)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var executablePath = Path.Combine(repositoryRoot, "artifacts", "bin", "native", "Release", "dearstory-host-cpp.exe");
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The native DearStory host executable was not found. Build the host before running conformance tests.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--pipe {pipeName} --host-id {hostId}",
            WorkingDirectory = repositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The native DearStory host process could not be started.");
    }

    private static Process StartManagedHost(string pipeName, string hostId)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var executablePath = Path.Combine(
            repositoryRoot,
            "src",
            "hosts",
            "dotnet",
            "DearStory.Host",
            "bin",
            "Release",
            "net10.0",
            "DearStory.Host.exe");

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The managed DearStory host executable was not found. Build the host before running conformance tests.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--pipe {pipeName} --host-id {hostId}",
            WorkingDirectory = repositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The managed DearStory host process could not be started.");
    }

    private static async Task<ControlEnvelope> ReadHandshakeHelloAsync(NamedPipeConnection connection, CancellationToken cancellationToken)
    {
        var payloadBytes = await connection.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The host disconnected before sending a hello envelope.");

        var decodeResult = ControlCodec.Decode(payloadBytes);
        if (!decodeResult.IsSuccess)
        {
            throw new InvalidOperationException(decodeResult.Error!.Message);
        }

        var envelope = decodeResult.Value!;
        if (!string.Equals(envelope.Type, "hello", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The first host message must be 'hello', not '{envelope.Type}'.");
        }

        return envelope;
    }

    private static ValueTask WriteWelcomeAsync(NamedPipeConnection connection, ControlEnvelope helloEnvelope, CancellationToken cancellationToken)
    {
        var payload = new Welcome
        {
            AcceptedCapabilities = ["control.handshake.v1", "story.run"],
            NegotiatedVersion = new GeneratedProtocolVersion
            {
                Major = ProtocolVersion.Major,
                Minor = ProtocolVersion.Minor
            },
            PeerId = Guid.Parse("11111111-1111-4111-8111-111111111111")
        };

        var welcome = new ControlEnvelope(
            ProtocolVersion,
            "welcome",
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            DateTimeOffset.UtcNow,
            payload,
            correlationId: helloEnvelope.MessageId);

        return connection.WriteAsync(ControlCodec.Encode(welcome), cancellationToken);
    }

    private async ValueTask<HarnessEnvelope> ReadEnvelopeAsync(NamedPipeConnection connection, CancellationToken cancellationToken)
    {
        var payload = await connection.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(await BuildDisconnectMessageAsync().ConfigureAwait(false));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var payloadJson = root.GetProperty("payload").GetRawText();

        return new HarnessEnvelope(
            root.GetProperty("type").GetString() ?? throw new InvalidOperationException("The control envelope type is missing."),
            Guid.Parse(root.GetProperty("messageId").GetString() ?? throw new InvalidOperationException("The control envelope messageId is missing.")),
            TryReadGuid(root, "correlationId"),
            TryReadGuid(root, "sessionId"),
            payloadJson);
    }

    private async Task<string> BuildDisconnectMessageAsync()
    {
        string? standardOutput = null;
        string? standardError = null;
        int? exitCode = null;

        if (_process.WaitForExit(1000))
        {
            exitCode = _process.ExitCode;
            standardOutput = await _process.StandardOutput.ReadToEndAsync(_lifetime.Token).ConfigureAwait(false);
            standardError = await _process.StandardError.ReadToEndAsync(_lifetime.Token).ConfigureAwait(false);
        }

        return $"The host disconnected while the harness was waiting for a control message. ExitCode={exitCode?.ToString(CultureInfo.InvariantCulture) ?? "running"} Stdout={standardOutput ?? "<unavailable>"} Stderr={standardError ?? "<unavailable>"}";
    }

    private static ValueTask WriteEnvelopeAsync<TPayload>(
        NamedPipeConnection connection,
        string type,
        TPayload payload,
        Guid? correlationId = null,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = new JsonObject
        {
            ["protocol"] = new JsonObject
            {
                ["major"] = ProtocolVersion.Major,
                ["minor"] = ProtocolVersion.Minor
            },
            ["type"] = type,
            ["messageId"] = Guid.NewGuid().ToString("D"),
            ["timestamp"] = FormatTimestamp(DateTimeOffset.UtcNow),
            ["payload"] = JsonSerializer.SerializeToNode(payload)
        };

        if (correlationId.HasValue)
        {
            envelope["correlationId"] = correlationId.Value.ToString("D");
        }

        if (sessionId.HasValue)
        {
            envelope["sessionId"] = sessionId.Value.ToString("D");
        }

        var payloadBytes = Encoding.UTF8.GetBytes(envelope.ToJsonString());
        return connection.WriteAsync(payloadBytes, cancellationToken);
    }

    private static Guid? TryReadGuid(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? Guid.Parse(value.GetString()!)
            : null;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    private static string ResolveRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var directory = new DirectoryInfo(current); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The DearStory repository root could not be located.");
    }

    internal sealed record HostStoryDescriptor(string CanonicalId, string Title);

    internal sealed record HostFrameSnapshot(int Width, int Height, long Sequence, ReadOnlyMemory<byte> Bytes);

    private sealed record HarnessEnvelope(
        string Type,
        Guid MessageId,
        Guid? CorrelationId,
        Guid? SessionId,
        string PayloadJson)
    {
        public T DeserializePayload<T>()
        {
            return JsonSerializer.Deserialize<T>(PayloadJson)
                ?? throw new InvalidOperationException($"The payload for '{Type}' could not be deserialized as {typeof(T).Name}.");
        }
    }
}
