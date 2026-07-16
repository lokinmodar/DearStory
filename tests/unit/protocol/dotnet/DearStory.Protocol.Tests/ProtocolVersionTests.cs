using DearStory.Protocol;
using Xunit;

namespace DearStory.Protocol.Tests;

public sealed class ProtocolVersionTests
{
    [Fact]
    public void Negotiate_UsesLowerMinor_WhenMajorMatches()
    {
        var negotiated = new ProtocolVersion(1, 0).Negotiate(new(1, 3));

        Assert.Equal(new ProtocolVersion(1, 0), negotiated);
        Assert.Null(new ProtocolVersion(2, 0).Negotiate(new(1, 0)));
    }
}
