namespace DearStory.ProtocolGenerator;

/// <summary>Represents a protocol manifest validation failure.</summary>
public sealed class ManifestException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ManifestException" /> class.</summary>
    /// <param name="code">A stable machine-readable error code.</param>
    /// <param name="message">A human-readable description of the validation failure.</param>
    public ManifestException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Initializes a new instance of the <see cref="ManifestException" /> class.</summary>
    /// <param name="code">A stable machine-readable error code.</param>
    /// <param name="message">A human-readable description of the validation failure.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public ManifestException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Gets the stable machine-readable error code.</summary>
    public string Code { get; }
}
