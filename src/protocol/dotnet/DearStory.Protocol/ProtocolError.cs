using System.Text.Json.Nodes;

namespace DearStory.Protocol;

/// <summary>Represents a protocol validation or transport failure.</summary>
public sealed class ProtocolError
{
    /// <summary>Initializes a new instance of the <see cref="ProtocolError" /> class.</summary>
    /// <param name="code">A stable machine-readable error code.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    /// <param name="recovery">A suggested recovery action for the caller.</param>
    /// <param name="details">Optional structured diagnostic context.</param>
    public ProtocolError(string code, string message, string recovery, JsonObject? details = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(recovery);

        Code = code;
        Message = message;
        Recovery = recovery;
        Details = details;
    }

    /// <summary>Gets the stable machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable description of the failure.</summary>
    public string Message { get; }

    /// <summary>Gets the suggested recovery action for the caller.</summary>
    public string Recovery { get; }

    /// <summary>Gets the optional structured diagnostic context.</summary>
    public JsonObject? Details { get; }
}

/// <summary>Represents a framing failure raised by the managed transport layer.</summary>
public sealed class ProtocolException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ProtocolException" /> class.</summary>
    /// <param name="code">A stable machine-readable error code.</param>
    /// <param name="message">A human-readable description of the framing failure.</param>
    public ProtocolException(string code, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
    }

    /// <summary>Gets the stable machine-readable error code.</summary>
    public string Code { get; }
}
