using DearStory.Protocol;
using Xunit;

namespace DearStory.Protocol.Tests;

public sealed class PublicSurfaceTests
{
    [Fact]
    public void Protocol_assembly_does_not_export_Windows_transport_types()
    {
        var transportTypes = typeof(ProtocolVersion).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("DearStory.Protocol.Windows", StringComparison.Ordinal) is true);

        Assert.Empty(transportTypes);
    }
}
