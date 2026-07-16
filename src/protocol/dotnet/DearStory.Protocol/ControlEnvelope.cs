using DearStory.Protocol.Generated;

namespace DearStory.Protocol;

/// <summary>Represents a decoded DearStory control envelope.</summary>
public sealed class ControlEnvelope
{
    /// <summary>Initializes a new instance of the <see cref="ControlEnvelope" /> class.</summary>
    /// <param name="protocol">A negotiated or declared protocol version.</param>
    /// <param name="type">The control message type.</param>
    /// <param name="messageId">A unique message identifier.</param>
    /// <param name="timestamp">The RFC 3339 UTC timestamp carried by the envelope.</param>
    /// <param name="payload">The typed control payload selected by <paramref name="type" />.</param>
    /// <param name="correlationId">An optional correlation identifier.</param>
    /// <param name="sessionId">An optional session identifier.</param>
    public ControlEnvelope(
        ProtocolVersion protocol,
        string type,
        Guid messageId,
        DateTimeOffset timestamp,
        object payload,
        Guid? correlationId = null,
        Guid? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);

        Protocol = protocol;
        Type = type;
        MessageId = messageId;
        CorrelationId = correlationId;
        SessionId = sessionId;
        Timestamp = timestamp;
        Payload = payload;
    }

    /// <summary>Gets the declared envelope protocol version.</summary>
    public ProtocolVersion Protocol { get; }

    /// <summary>Gets the control message type.</summary>
    public string Type { get; }

    /// <summary>Gets the unique message identifier.</summary>
    public Guid MessageId { get; }

    /// <summary>Gets the optional correlation identifier.</summary>
    public Guid? CorrelationId { get; }

    /// <summary>Gets the optional session identifier.</summary>
    public Guid? SessionId { get; }

    /// <summary>Gets the RFC 3339 UTC timestamp.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the typed payload selected by <see cref="Type" />.</summary>
    public object Payload { get; }
}
