using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DearStory.Capture;
using DearStory.Protocol;
using DearStory.Protocol.Generated;
using DearStory.Protocol.Windows;
using DearStory.Runner.Configuration;
using DearStory.Transport.Windows;
using GeneratedProtocolVersion = DearStory.Protocol.Generated.ProtocolVersion;
using HostProtocolVersion = DearStory.Protocol.ProtocolVersion;

namespace DearStory.Runner.Capture;

/// <summary>
/// Owns one real host process plus its named-pipe and shared-memory frame-capture session lifecycle.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RunnerHostCaptureSession : IAsyncDisposable
{
    private static readonly HostProtocolVersion SupportedProtocolVersion = new(1, 0);
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private readonly NamedPipeControlServer _server;
    private readonly NamedPipeConnection _connection;
    private readonly Process _process;
    private readonly CancellationTokenSource _lifetime;

    private RunnerHostCaptureSession(
        string hostId,
        NamedPipeControlServer server,
        NamedPipeConnection connection,
        Process process,
        CancellationTokenSource lifetime,
        IReadOnlyList<string> publishedStoryIds)
    {
        HostId = hostId;
        _server = server;
        _connection = connection;
        _process = process;
        _lifetime = lifetime;
        PublishedStoryIds = publishedStoryIds;
    }

    /// <summary>
    /// Gets the stable host identifier for the connected host process.
    /// </summary>
    /// <value>The configured host identifier.</value>
    public string HostId { get; }

    /// <summary>
    /// Gets the published story identifiers discovered during session startup.
    /// </summary>
    /// <value>The ordered story identifiers published by the connected host.</value>
    public IReadOnlyList<string> PublishedStoryIds { get; }

    /// <summary>
    /// Starts one host process, completes the control handshake, and reads its published story index.
    /// </summary>
    /// <param name="configuration">The workspace configuration that owns the host definition.</param>
    /// <param name="host">The host definition to start.</param>
    /// <param name="buildConfiguration">The build configuration that resolves the host executable artifacts.</param>
    /// <param name="cancellationToken">The cancellation token that aborts startup.</param>
    /// <returns>A connected host capture session.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> or <paramref name="host" /> is <see langword="null" />.</exception>
    public static async Task<RunnerHostCaptureSession> StartAsync(
        WorkspaceConfiguration configuration,
        HostConfiguration host,
        string buildConfiguration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildConfiguration);

        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(20));

        var pipeName = $"dearstory-host-{Guid.NewGuid():N}";
        var server = new NamedPipeControlServer(pipeName);
        var repositoryRoot = ResolveRepositoryRoot(configuration.Workspace.RootPath);
        var process = StartHost(repositoryRoot, pipeName, host.Id, buildConfiguration);

        try
        {
            var connection = await server.AcceptAsync(lifetime.Token).ConfigureAwait(false);
            var helloEnvelope = await ReadHandshakeHelloAsync(connection, lifetime.Token).ConfigureAwait(false);
            await WriteWelcomeAsync(connection, helloEnvelope, lifetime.Token).ConfigureAwait(false);

            var publishedStoryIds = await WaitForStoryIndexAsync(connection, lifetime.Token).ConfigureAwait(false);
            return new RunnerHostCaptureSession(host.Id, server, connection, process, lifetime, publishedStoryIds);
        }
        catch
        {
            lifetime.Dispose();
            await server.DisposeAsync().ConfigureAwait(false);

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens one story session on the connected host and reads its latest RGBA frame.
    /// </summary>
    /// <param name="storyId">The story identifier to capture.</param>
    /// <param name="backend">One of the enumeration values that specifies the capture backend.</param>
    /// <param name="cancellationToken">The cancellation token that aborts capture.</param>
    /// <returns>The captured RGBA frame.</returns>
    /// <exception cref="ArgumentException"><paramref name="storyId" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The requested story is not published by this host, the host disconnects, or the frame channel does not yield a readable frame.</exception>
    public async Task<CapturedFrame> CaptureAsync(string storyId, CaptureBackendKind backend, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storyId))
        {
            throw new ArgumentException("A story identifier must be provided.", nameof(storyId));
        }

        if (!PublishedStoryIds.Contains(storyId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The host '{HostId}' does not publish story '{storyId}'.");
        }

        _ = backend;

        using var linkedLifetime = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        var linkedToken = linkedLifetime.Token;
        var sessionId = Guid.NewGuid();
        var observedTypes = new List<string>();

        var openPayload = new StorySessionOpen
        {
            InitialArguments = JsonNode.Parse("{}")!,
            RandomSeed = "1",
            SessionId = sessionId,
            StartTimeUtc = FormatTimestamp(DateTimeOffset.UtcNow),
            StoryId = storyId,
        };

        await WriteEnvelopeAsync(
            _connection,
            "story_session_open",
            openPayload,
            sessionId: sessionId,
            cancellationToken: linkedToken).ConfigureAwait(false);

        FrameChannelReady? frameChannel = null;

        try
        {
            while (true)
            {
                var envelope = await ReadEnvelopeAsync(linkedToken).ConfigureAwait(false);
                observedTypes.Add(envelope.Type);
                if (envelope.SessionId != sessionId)
                {
                    continue;
                }

                if (string.Equals(envelope.Type, "frame_channel_ready", StringComparison.Ordinal))
                {
                    frameChannel = envelope.DeserializePayload<FrameChannelReady>();
                    continue;
                }

                if (string.Equals(envelope.Type, "frame_presented", StringComparison.Ordinal))
                {
                    if (frameChannel is null)
                    {
                        throw new InvalidOperationException("A frame was presented before the host published a frame channel descriptor.");
                    }

                    var presented = envelope.DeserializePayload<FramePresented>();
                    var descriptor = FrameTransportDescriptor.Create(
                        frameChannel.MappingName,
                        frameChannel.Width,
                        frameChannel.Height,
                        frameChannel.Stride,
                        frameChannel.SlotCount);

                    using var reader = new SharedMemoryFrameReader(descriptor);
                    if (!reader.TryReadLatest(out var frame))
                    {
                        throw new InvalidOperationException("The shared-memory frame channel did not expose a readable frame.");
                    }

                    return new CapturedFrame(
                        storyId,
                        HostId,
                        frameChannel.Width,
                        frameChannel.Height,
                        frameChannel.Stride,
                        frame.Bytes,
                        ParseTimestamp(presented.TimestampUtc));
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

    /// <inheritdoc />
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

    private static Process StartHost(string repositoryRoot, string pipeName, string hostId, string buildConfiguration) =>
        hostId switch
        {
            "cpp-host" => StartNativeHost(repositoryRoot, pipeName, hostId, buildConfiguration),
            "dotnet-host" => StartManagedHost(repositoryRoot, pipeName, hostId, buildConfiguration),
            _ => throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "The requested host is not supported by the runner capture session."),
        };

    private static Process StartNativeHost(string repositoryRoot, string pipeName, string hostId, string buildConfiguration)
    {
        var executablePath = HostArtifactPathResolver.ResolveNativeHostExecutable(repositoryRoot, buildConfiguration);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The native DearStory host executable was not found. Run the standard build before executing visual capture.", executablePath);
        }

        return StartProcess(executablePath, repositoryRoot, pipeName, hostId);
    }

    private static Process StartManagedHost(string repositoryRoot, string pipeName, string hostId, string buildConfiguration)
    {
        var executablePath = HostArtifactPathResolver.ResolveManagedHostExecutable(repositoryRoot, buildConfiguration);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The managed DearStory host executable was not found. Run the standard build before executing visual capture.", executablePath);
        }

        return StartProcess(executablePath, repositoryRoot, pipeName, hostId);
    }

    private static Process StartProcess(string executablePath, string workingDirectory, string pipeName, string hostId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--pipe {pipeName} --host-id {hostId}",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"The host process '{executablePath}' could not be started.");
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
                Major = SupportedProtocolVersion.Major,
                Minor = SupportedProtocolVersion.Minor,
            },
            PeerId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
        };

        var welcome = new ControlEnvelope(
            SupportedProtocolVersion,
            "welcome",
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            DateTimeOffset.UtcNow,
            payload,
            correlationId: helloEnvelope.MessageId);

        return connection.WriteAsync(ControlCodec.Encode(welcome), cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> WaitForStoryIndexAsync(NamedPipeConnection connection, CancellationToken cancellationToken)
    {
        while (true)
        {
            var envelope = await ReadEnvelopeAsync(connection, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(envelope.Type, "story_index_published", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = envelope.DeserializePayload<StoryIndexPublished>();
            return payload.Stories
                .Select(static story => story.Id)
                .ToArray();
        }
    }

    private async Task<HarnessEnvelope> ReadEnvelopeAsync(CancellationToken cancellationToken)
    {
        return await ReadEnvelopeAsync(_connection, cancellationToken).ConfigureAwait(false);
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

        return $"The host '{HostId}' disconnected while the runner was waiting for a control message. ExitCode={exitCode?.ToString(CultureInfo.InvariantCulture) ?? "running"} Stdout={standardOutput ?? "<unavailable>"} Stderr={standardError ?? "<unavailable>"}";
    }

    private static async Task<HarnessEnvelope> ReadEnvelopeAsync(NamedPipeConnection connection, CancellationToken cancellationToken)
    {
        var payload = await connection.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The host disconnected while the runner was waiting for a control message.");

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
                ["major"] = SupportedProtocolVersion.Major,
                ["minor"] = SupportedProtocolVersion.Minor,
            },
            ["type"] = type,
            ["messageId"] = Guid.NewGuid().ToString("D"),
            ["timestamp"] = FormatTimestamp(DateTimeOffset.UtcNow),
            ["payload"] = JsonSerializer.SerializeToNode(payload),
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
        return value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.ParseExact(
            value,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static string ResolveRepositoryRoot(string workspaceRootPath)
    {
        var current = Path.GetFullPath(workspaceRootPath);
        for (var directory = new DirectoryInfo(current); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"The DearStory repository root could not be located from '{workspaceRootPath}'.");
    }

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
