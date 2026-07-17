namespace DearStory.Runner.Diagnostics;

/// <summary>Represents one structured diagnostic entry emitted by the Windows runner.</summary>
public sealed class StructuredDiagnostic
{
    /// <summary>Initializes a new instance of the <see cref="StructuredDiagnostic" /> class.</summary>
    /// <param name="timestampUtc">The UTC timestamp for the diagnostic entry.</param>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="message">The human-readable diagnostic message.</param>
    public StructuredDiagnostic(DateTimeOffset timestampUtc, string code, StructuredDiagnosticSeverity severity, string message)
    {
        TimestampUtc = timestampUtc;
        Code = code;
        Severity = severity;
        Message = message;
    }

    /// <summary>Gets the UTC timestamp for the diagnostic entry.</summary>
    /// <value>The UTC timestamp associated with the diagnostic entry.</value>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>Gets the stable diagnostic code.</summary>
    /// <value>The stable code that categorizes the diagnostic event.</value>
    public string Code { get; }

    /// <summary>Gets the diagnostic severity.</summary>
    /// <value>The severity of the diagnostic event.</value>
    public StructuredDiagnosticSeverity Severity { get; }

    /// <summary>Gets the human-readable diagnostic message.</summary>
    /// <value>The message that explains the diagnostic event.</value>
    public string Message { get; }
}

/// <summary>Defines the severity levels emitted by the Windows runner.</summary>
public enum StructuredDiagnosticSeverity
{
    /// <summary>Indicates an informational event.</summary>
    Info,

    /// <summary>Indicates a warning event that does not immediately terminate the runner.</summary>
    Warning,

    /// <summary>Indicates an error event.</summary>
    Error
}
