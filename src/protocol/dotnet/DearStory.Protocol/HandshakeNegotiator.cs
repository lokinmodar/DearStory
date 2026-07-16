using DearStory.Protocol.Generated;
using GeneratedProtocolError = DearStory.Protocol.Generated.ProtocolError;

namespace DearStory.Protocol;

/// <summary>Describes the local peer data required to negotiate one hello envelope.</summary>
public sealed class HandshakePolicy
{
    /// <summary>Gets the local protocol version supported by this peer.</summary>
    public required ProtocolVersion LocalVersion { get; init; }

    /// <summary>Gets the local implementation identity echoed into diagnostics.</summary>
    public required ImplementationIdentity LocalImplementation { get; init; }

    /// <summary>Gets the capabilities supported by the local peer.</summary>
    public required IReadOnlyList<string> SupportedCapabilities { get; init; }

    /// <summary>Gets the delegate that produces response identifiers.</summary>
    public required Func<Guid> NextGuid { get; init; }

    /// <summary>Gets the delegate that produces UTC response timestamps.</summary>
    public required Func<DateTimeOffset> UtcNow { get; init; }
}

/// <summary>Negotiates hello control envelopes into welcome or reject envelopes.</summary>
public static class HandshakeNegotiator
{
    /// <summary>Negotiates a hello control envelope into a welcome or reject envelope.</summary>
    /// <param name="helloEnvelope">The remote hello envelope to evaluate.</param>
    /// <param name="policy">The local policy and identity used during negotiation.</param>
    /// <returns>A welcome envelope when negotiation succeeds; otherwise, a reject envelope.</returns>
    public static ControlEnvelope Negotiate(ControlEnvelope helloEnvelope, HandshakePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(helloEnvelope);
        ArgumentNullException.ThrowIfNull(policy);

        if (helloEnvelope.Type != "hello" || helloEnvelope.Payload is not Hello hello)
        {
            return CreateReject(
                helloEnvelope,
                policy,
                "protocol.invalid_envelope",
                "The handshake requires a hello control envelope.",
                "Resend a valid hello envelope.");
        }

        if (HasDuplicates(hello.SupportedCapabilities) || HasDuplicates(hello.RequiredCapabilities))
        {
            return CreateReject(
                helloEnvelope,
                policy,
                "protocol.invalid_envelope",
                "The hello envelope contains duplicate capabilities.",
                "Resend the hello envelope with unique capability names.");
        }

        var negotiatedVersion = policy.LocalVersion.Negotiate(helloEnvelope.Protocol);
        if (negotiatedVersion is null)
        {
            return CreateReject(
                helloEnvelope,
                policy,
                "protocol.major_mismatch",
                "The remote peer uses an unsupported protocol major.",
                $"Retry with protocol {policy.LocalVersion.Major}.{policy.LocalVersion.Minor}.");
        }

        var localSupported = policy.SupportedCapabilities.OrderBy(static capability => capability, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var requiredCapability in hello.RequiredCapabilities)
        {
            if (Array.BinarySearch(localSupported, requiredCapability, StringComparer.Ordinal) < 0)
            {
                return CreateReject(
                    helloEnvelope,
                    policy,
                    "protocol.required_capability_missing",
                    $"The remote peer requires an unsupported capability: {requiredCapability}.",
                    $"Retry after enabling the required capability: {requiredCapability}.");
            }
        }

        var acceptedCapabilities = localSupported
            .Intersect(hello.SupportedCapabilities, StringComparer.Ordinal)
            .OrderBy(static capability => capability, StringComparer.Ordinal)
            .ToArray();

        var messageId = policy.NextGuid();
        var sessionId = policy.NextGuid();
        var peerId = policy.NextGuid();

        return new ControlEnvelope(
            negotiatedVersion.Value,
            "welcome",
            messageId,
            policy.UtcNow(),
            new Welcome
            {
                AcceptedCapabilities = acceptedCapabilities,
                NegotiatedVersion = new Generated.ProtocolVersion
                {
                    Major = negotiatedVersion.Value.Major,
                    Minor = negotiatedVersion.Value.Minor
                },
                PeerId = peerId
            },
            correlationId: helloEnvelope.MessageId,
            sessionId: sessionId);
    }

    private static bool HasDuplicates(IReadOnlyList<string> values) =>
        values.Count != values.Distinct(StringComparer.Ordinal).Count();

    private static ControlEnvelope CreateReject(
        ControlEnvelope helloEnvelope,
        HandshakePolicy policy,
        string code,
        string message,
        string recovery) =>
        new(
            policy.LocalVersion,
            "reject",
            policy.NextGuid(),
            policy.UtcNow(),
            new Reject
            {
                Error = new GeneratedProtocolError
                {
                    Code = code,
                    Message = message,
                    Recovery = recovery
                }
            },
            correlationId: helloEnvelope.MessageId);
}
