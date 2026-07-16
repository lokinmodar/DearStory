using DearStory.Protocol.Generated;
using Xunit;

namespace DearStory.Protocol.Tests;

public sealed class HandshakeNegotiatorTests
{
    [Fact]
    public void Negotiate_accepts_protocol_1_0_and_preserves_correlation()
    {
        var policy = CreatePolicy(
            new ProtocolVersion(1, 0),
            [ "control.handshake.v1", "story.run", "visual.snapshot" ]);
        var hello = CreateHelloEnvelope(
            new ProtocolVersion(1, 0),
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            [ "visual.snapshot", "control.handshake.v1", "story.run" ],
            [ "control.handshake.v1" ]);

        var response = HandshakeNegotiator.Negotiate(hello, policy);

        Assert.Equal("welcome", response.Type);
        Assert.Equal(new ProtocolVersion(1, 0), response.Protocol);
        Assert.Equal(Guid.Parse("11111111-1111-4111-8111-111111111111"), response.MessageId);
        Assert.Equal(hello.MessageId, response.CorrelationId);
        Assert.Equal(Guid.Parse("22222222-2222-4222-8222-222222222222"), response.SessionId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-16T00:00:01.000Z"), response.Timestamp);

        var welcome = Assert.IsType<Welcome>(response.Payload);
        Assert.Equal(Guid.Parse("33333333-3333-4333-8333-333333333333"), welcome.PeerId);
        Assert.Equal(
            [ "control.handshake.v1", "story.run", "visual.snapshot" ],
            welcome.AcceptedCapabilities);
    }

    [Fact]
    public void Negotiate_uses_the_lower_minor_when_majors_match()
    {
        var policy = CreatePolicy(
            new ProtocolVersion(1, 3),
            [ "control.handshake.v1", "story.run" ]);
        var hello = CreateHelloEnvelope(
            new ProtocolVersion(1, 1),
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            [ "story.run", "control.handshake.v1" ],
            [ "control.handshake.v1" ]);

        var response = HandshakeNegotiator.Negotiate(hello, policy);

        Assert.Equal("welcome", response.Type);
        Assert.Equal(new ProtocolVersion(1, 1), response.Protocol);

        var welcome = Assert.IsType<Welcome>(response.Payload);
        Assert.Equal(1, welcome.NegotiatedVersion.Major);
        Assert.Equal(1, welcome.NegotiatedVersion.Minor);
    }

    [Fact]
    public void Negotiate_rejects_a_protocol_major_mismatch()
    {
        var policy = CreatePolicy(
            new ProtocolVersion(1, 0),
            [ "control.handshake.v1" ]);
        var hello = CreateHelloEnvelope(
            new ProtocolVersion(2, 0),
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            [ "control.handshake.v1" ],
            [ "control.handshake.v1" ]);

        var response = HandshakeNegotiator.Negotiate(hello, policy);

        Assert.Equal("reject", response.Type);

        var reject = Assert.IsType<Reject>(response.Payload);
        Assert.Equal("protocol.major_mismatch", reject.Error.Code);
    }

    [Fact]
    public void Negotiate_rejects_a_missing_required_capability()
    {
        var policy = CreatePolicy(
            new ProtocolVersion(1, 0),
            [ "control.handshake.v1" ]);
        var hello = CreateHelloEnvelope(
            new ProtocolVersion(1, 0),
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
            [ "control.handshake.v1", "story.run" ],
            [ "control.handshake.v1", "visual.snapshot" ]);

        var response = HandshakeNegotiator.Negotiate(hello, policy);

        Assert.Equal("reject", response.Type);

        var reject = Assert.IsType<Reject>(response.Payload);
        Assert.Equal("protocol.required_capability_missing", reject.Error.Code);
    }

    [Fact]
    public void Negotiate_rejects_duplicate_capabilities()
    {
        var policy = CreatePolicy(
            new ProtocolVersion(1, 0),
            [ "control.handshake.v1", "story.run" ]);
        var hello = CreateHelloEnvelope(
            new ProtocolVersion(1, 0),
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
            [ "control.handshake.v1", "story.run", "story.run" ],
            [ "control.handshake.v1" ]);

        var response = HandshakeNegotiator.Negotiate(hello, policy);

        Assert.Equal("reject", response.Type);

        var reject = Assert.IsType<Reject>(response.Payload);
        Assert.Equal("protocol.invalid_envelope", reject.Error.Code);
    }

    private static HandshakePolicy CreatePolicy(ProtocolVersion localVersion, IReadOnlyList<string> supportedCapabilities)
    {
        var ids = new Queue<Guid>([
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333")]);

        return new HandshakePolicy
        {
            LocalImplementation = CreateIdentity("csharp"),
            LocalVersion = localVersion,
            SupportedCapabilities = supportedCapabilities,
            NextGuid = () => ids.Dequeue(),
            UtcNow = () => DateTimeOffset.Parse("2026-07-16T00:00:01.000Z")
        };
    }

    private static ControlEnvelope CreateHelloEnvelope(
        ProtocolVersion protocol,
        Guid messageId,
        IReadOnlyList<string> supportedCapabilities,
        IReadOnlyList<string> requiredCapabilities) =>
        new(
            protocol,
            "hello",
            messageId,
            DateTimeOffset.Parse("2026-07-16T00:00:00.000Z"),
            new Hello
            {
                Implementation = CreateIdentity("csharp"),
                RequiredCapabilities = requiredCapabilities,
                Role = PeerRole.Host,
                SupportedCapabilities = supportedCapabilities
            });

    private static ImplementationIdentity CreateIdentity(string language) =>
        new()
        {
            Language = language,
            Name = "DearStory.Test",
            Toolchain = "test-toolchain",
            Version = "1.0.0"
        };
}
