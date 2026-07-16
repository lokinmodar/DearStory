using System.Text.Json;
using DearStory.Protocol.Generated;
using Xunit;

namespace DearStory.Protocol.Tests;

public sealed class ControlCodecTests
{
    [Fact]
    public void Decode_round_trips_canonical_hello()
    {
        var json = TestVectors.ReadBytes("hello.valid.json");

        var decoded = ControlCodec.Decode(json);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.IsType<Hello>(decoded.Value!.Payload);
        Assert.True(JsonSemanticComparer.Equals(json, ControlCodec.Encode(decoded.Value)));
    }

    [Fact]
    public void Decode_rejects_an_envelope_without_message_id()
    {
        var json = TestVectors.ReadBytes("hello.missing-message-id.json");

        var decoded = ControlCodec.Decode(json);

        Assert.False(decoded.IsSuccess);
        Assert.Equal("protocol.invalid_envelope", decoded.Error!.Code);
    }

    [Fact]
    public void Decode_preserves_a_higher_protocol_major_for_handshake_rejection()
    {
        var json = """{"protocol":{"major":2,"minor":0},"type":"hello","messageId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00.000Z","payload":{"role":"host","implementation":{"name":"DearStory.Test","version":"1.0.0","language":"csharp","toolchain":"test-toolchain"},"supportedCapabilities":["control.handshake.v1"],"requiredCapabilities":["control.handshake.v1"]}}"""u8.ToArray();

        var decoded = ControlCodec.Decode(json);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.Equal(new ProtocolVersion(2, 0), decoded.Value!.Protocol);
    }

    [Fact]
    public void Encode_omits_null_optional_fields_from_reject_payloads()
    {
        var envelope = new ControlEnvelope(
            new ProtocolVersion(1, 0),
            "reject",
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DateTimeOffset.Parse("2026-07-16T00:00:00.000Z"),
            new Reject
            {
                Error = new Generated.ProtocolError
                {
                    Code = "protocol.required_capability_missing",
                    Message = "Missing capability.",
                    Recovery = "Enable the capability."
                }
            });

        var encoded = ControlCodec.Encode(envelope);
        using var document = JsonDocument.Parse(encoded);

        Assert.False(document.RootElement.GetProperty("payload").GetProperty("error").TryGetProperty("details", out _));
    }

    [Fact]
    public void Decode_round_trips_a_welcome_envelope_with_correlation_metadata()
    {
        var json = """{"protocol":{"major":1,"minor":0},"type":"welcome","messageId":"22222222-2222-4222-8222-222222222222","correlationId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00.100Z","payload":{"peerId":"33333333-3333-4333-8333-333333333333","negotiatedVersion":{"major":1,"minor":0},"acceptedCapabilities":["control.handshake.v1"]}}"""u8.ToArray();

        var decoded = ControlCodec.Decode(json);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.IsType<Welcome>(decoded.Value!.Payload);
        Assert.True(JsonSemanticComparer.Equals(json, ControlCodec.Encode(decoded.Value)));
    }

    [Fact]
    public void Decode_round_trips_a_reject_envelope_with_details_and_session_metadata()
    {
        var json = """{"protocol":{"major":1,"minor":0},"type":"reject","messageId":"22222222-2222-4222-8222-222222222222","sessionId":"33333333-3333-4333-8333-333333333333","timestamp":"2026-07-16T00:00:00.100Z","payload":{"error":{"code":"protocol.required_capability_missing","message":"Missing capability.","recovery":"Retry with control.handshake.v1.","details":{"missingCapability":"control.handshake.v1"}}}}"""u8.ToArray();

        var decoded = ControlCodec.Decode(json);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.IsType<Reject>(decoded.Value!.Payload);
        Assert.True(JsonSemanticComparer.Equals(json, ControlCodec.Encode(decoded.Value)));
    }

    [Fact]
    public void Decode_rejects_an_invalid_timestamp_shape()
    {
        var json = """{"protocol":{"major":1,"minor":0},"type":"hello","messageId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00Z","payload":{"role":"host","implementation":{"name":"DearStory.Test","version":"1.0.0","language":"csharp","toolchain":"test-toolchain"},"supportedCapabilities":["control.handshake.v1"],"requiredCapabilities":["control.handshake.v1"]}}"""u8.ToArray();

        var decoded = ControlCodec.Decode(json);

        Assert.False(decoded.IsSuccess);
        Assert.Equal("protocol.invalid_envelope", decoded.Error!.Code);
    }

    [Fact]
    public void Decode_rejects_an_invalid_peer_role()
    {
        var json = """{"protocol":{"major":1,"minor":0},"type":"hello","messageId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00.000Z","payload":{"role":"invalid","implementation":{"name":"DearStory.Test","version":"1.0.0","language":"csharp","toolchain":"test-toolchain"},"supportedCapabilities":["control.handshake.v1"],"requiredCapabilities":["control.handshake.v1"]}}"""u8.ToArray();

        var decoded = ControlCodec.Decode(json);

        Assert.False(decoded.IsSuccess);
        Assert.Equal("protocol.invalid_envelope", decoded.Error!.Code);
    }
}
