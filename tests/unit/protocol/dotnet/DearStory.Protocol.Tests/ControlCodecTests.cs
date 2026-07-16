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
}
