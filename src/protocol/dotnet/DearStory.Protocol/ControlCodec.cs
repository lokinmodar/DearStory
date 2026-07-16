using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DearStory.Protocol.Generated;
using GeneratedProtocolError = DearStory.Protocol.Generated.ProtocolError;
using GeneratedProtocolVersion = DearStory.Protocol.Generated.ProtocolVersion;

namespace DearStory.Protocol;

/// <summary>Provides managed DearStory control envelope encoding and decoding.</summary>
public static class ControlCodec
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>Decodes a UTF-8 JSON control envelope.</summary>
    /// <param name="json">The UTF-8 JSON bytes to decode.</param>
    /// <returns>A success result containing the decoded envelope, or a failure result.</returns>
    public static DecodeResult Decode(ReadOnlySpan<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json, ReaderOptions);
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return DecodeResult.Failure(InvalidEnvelope("The control envelope must be a JSON object."));
            }

            if (!root.TryGetProperty("protocol", out var protocolElement) ||
                protocolElement.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("messageId", out var messageIdElement) ||
                messageIdElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("timestamp", out var timestampElement) ||
                timestampElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("payload", out var payloadElement) ||
                payloadElement.ValueKind != JsonValueKind.Object)
            {
                return DecodeResult.Failure(InvalidEnvelope("The control envelope is missing a required field."));
            }

            var protocol = ParseEnvelopeVersion(protocolElement);
            var type = typeElement.GetString()!;
            if (!TryReadLowercaseGuid(messageIdElement.GetString(), out var messageId))
            {
                return DecodeResult.Failure(InvalidEnvelope("The messageId field must be a lowercase RFC 4122 UUID."));
            }

            if (!TryReadTimestamp(timestampElement.GetString(), out var timestamp))
            {
                return DecodeResult.Failure(InvalidEnvelope("The timestamp field must be an RFC 3339 UTC timestamp with millisecond precision."));
            }

            Guid? correlationId = null;
            if (root.TryGetProperty("correlationId", out var correlationElement))
            {
                if (correlationElement.ValueKind != JsonValueKind.String ||
                    !TryReadLowercaseGuid(correlationElement.GetString(), out var parsedCorrelationId))
                {
                    return DecodeResult.Failure(InvalidEnvelope("The correlationId field must be a lowercase RFC 4122 UUID."));
                }

                correlationId = parsedCorrelationId;
            }

            Guid? sessionId = null;
            if (root.TryGetProperty("sessionId", out var sessionElement))
            {
                if (sessionElement.ValueKind != JsonValueKind.String ||
                    !TryReadLowercaseGuid(sessionElement.GetString(), out var parsedSessionId))
                {
                    return DecodeResult.Failure(InvalidEnvelope("The sessionId field must be a lowercase RFC 4122 UUID."));
                }

                sessionId = parsedSessionId;
            }

            object payload = type switch
            {
                "hello" => DeserializePayload<Hello>(payloadElement, ProtocolJsonContext.Default.Hello),
                "welcome" => DeserializePayload<Welcome>(payloadElement, ProtocolJsonContext.Default.Welcome),
                "reject" => DeserializePayload<Reject>(payloadElement, ProtocolJsonContext.Default.Reject),
                _ => throw new FormatException("The control envelope type is unsupported.")
            };

            return DecodeResult.Success(new ControlEnvelope(
                protocol,
                type,
                messageId,
                timestamp,
                payload,
                correlationId,
                sessionId));
        }
        catch (JsonException exception)
        {
            return DecodeResult.Failure(InvalidEnvelope(exception.Message));
        }
        catch (FormatException exception)
        {
            return DecodeResult.Failure(exception.Message.Contains("unsupported", StringComparison.Ordinal)
                ? new ProtocolError("protocol.unknown_message_type", exception.Message, "Use one of: hello, welcome, reject.")
                : InvalidEnvelope(exception.Message));
        }
    }

    /// <summary>Encodes a control envelope to UTF-8 JSON.</summary>
    /// <param name="envelope">The envelope to encode.</param>
    /// <returns>The encoded UTF-8 JSON bytes.</returns>
    public static byte[] Encode(ControlEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WritePropertyName("protocol");
        writer.WriteStartObject();
        writer.WriteNumber("major", envelope.Protocol.Major);
        writer.WriteNumber("minor", envelope.Protocol.Minor);
        writer.WriteEndObject();
        writer.WriteString("type", envelope.Type);
        writer.WriteString("messageId", envelope.MessageId);
        if (envelope.CorrelationId.HasValue)
        {
            writer.WriteString("correlationId", envelope.CorrelationId.Value);
        }

        if (envelope.SessionId.HasValue)
        {
            writer.WriteString("sessionId", envelope.SessionId.Value);
        }

        writer.WriteString("timestamp", envelope.Timestamp.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture));
        writer.WritePropertyName("payload");

        switch (envelope.Type)
        {
            case "hello" when envelope.Payload is Hello hello:
                JsonSerializer.Serialize(writer, hello, ProtocolJsonContext.Default.Hello);
                break;
            case "welcome" when envelope.Payload is Welcome welcome:
                JsonSerializer.Serialize(writer, welcome, ProtocolJsonContext.Default.Welcome);
                break;
            case "reject" when envelope.Payload is Reject reject:
                JsonSerializer.Serialize(writer, reject, ProtocolJsonContext.Default.Reject);
                break;
            default:
                throw new FormatException("The control envelope payload does not match the declared type.");
        }

        writer.WriteEndObject();
        writer.Flush();
        return stream.ToArray();
    }

    private static T DeserializePayload<T>(JsonElement payloadElement, JsonTypeInfo<T> typeInfo)
        where T : class =>
        JsonSerializer.Deserialize(payloadElement, typeInfo)
        ?? throw new FormatException("The payload could not be deserialized.");

    private static ProtocolVersion ParseEnvelopeVersion(JsonElement element)
    {
        if (!element.TryGetProperty("major", out var majorElement) ||
            !element.TryGetProperty("minor", out var minorElement) ||
            !majorElement.TryGetUInt16(out var major) ||
            !minorElement.TryGetUInt16(out var minor))
        {
            throw new FormatException("The protocol version object is invalid.");
        }

        if (major != ProtocolVersion.CurrentMajor)
        {
            throw new FormatException("The envelope protocol major is unsupported.");
        }

        return new ProtocolVersion(major, minor);
    }

    private static bool TryReadLowercaseGuid(string? text, out Guid value)
    {
        if (text is not null &&
            Guid.TryParseExact(text, "D", out value) &&
            string.Equals(text, value.ToString("D"), StringComparison.Ordinal))
        {
            return true;
        }

        value = Guid.Empty;
        return false;
    }

    private static bool TryReadTimestamp(string? text, out DateTimeOffset value)
    {
        if (text is not null &&
            DateTimeOffset.TryParseExact(
                text,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static ProtocolError InvalidEnvelope(string message) =>
        new("protocol.invalid_envelope", message, "Resend a valid DearStory control envelope.");
}

/// <summary>Represents the outcome of managed control envelope decoding.</summary>
public sealed class DecodeResult
{
    private DecodeResult(ControlEnvelope? value, ProtocolError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value that indicates whether decoding succeeded.</summary>
    public bool IsSuccess => Value is not null;

    /// <summary>Gets the decoded envelope when <see cref="IsSuccess" /> is <see langword="true" />.</summary>
    public ControlEnvelope? Value { get; }

    /// <summary>Gets the decoding error when <see cref="IsSuccess" /> is <see langword="false" />.</summary>
    public ProtocolError? Error { get; }

    /// <summary>Creates a successful decode result.</summary>
    /// <param name="value">The decoded envelope value.</param>
    /// <returns>A successful decode result.</returns>
    public static DecodeResult Success(ControlEnvelope value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DecodeResult(value, null);
    }

    /// <summary>Creates a failed decode result.</summary>
    /// <param name="error">The decoding error.</param>
    /// <returns>A failed decode result.</returns>
    public static DecodeResult Failure(ProtocolError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DecodeResult(null, error);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(Hello))]
[JsonSerializable(typeof(ImplementationIdentity))]
[JsonSerializable(typeof(PeerRole))]
[JsonSerializable(typeof(Reject))]
[JsonSerializable(typeof(GeneratedProtocolError))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(GeneratedProtocolVersion))]
[JsonSerializable(typeof(Welcome))]
internal sealed partial class ProtocolJsonContext : JsonSerializerContext;
