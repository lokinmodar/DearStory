namespace DearStory.Protocol;

/// <summary>Identifies a DearStory wire-protocol version.</summary>
/// <param name="Major">A breaking protocol generation.</param>
/// <param name="Minor">An additive protocol generation.</param>
public readonly record struct ProtocolVersion(ushort Major, ushort Minor)
{
    /// <summary>Gets the current protocol major.</summary>
    public const ushort CurrentMajor = 1;

    /// <summary>Gets the current protocol minor.</summary>
    public const ushort CurrentMinor = 0;

    /// <summary>Returns the shared version, or <see langword="null"/> for a major mismatch.</summary>
    /// <param name="other">Another protocol version to negotiate with.</param>
    /// <returns>A shared protocol version when both majors match; otherwise, <see langword="null"/>.</returns>
    public ProtocolVersion? Negotiate(ProtocolVersion other) =>
        Major == other.Major ? new(Major, Math.Min(Minor, other.Minor)) : null;
}
