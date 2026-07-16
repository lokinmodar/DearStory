using DearStory.ProtocolGenerator;
using Xunit;

namespace DearStory.ProtocolGenerator.Tests;

public sealed class ModelEmitterTests
{
    [Fact]
    public void Emit_is_deterministic_and_contains_all_wire_types()
    {
        var manifest = ProtocolManifest.Parse(TestManifest.Valid);

        var first = ModelEmitter.Emit(manifest);
        var second = ModelEmitter.Emit(manifest);

        Assert.Equal(first, second);
        Assert.Contains("struct hello final", first.Cpp, StringComparison.Ordinal);
        Assert.Contains("public sealed record Hello", first.CSharp, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", first.CSharp, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_duplicate_message_names()
    {
        var error = Assert.Throws<ManifestException>(() =>
            ProtocolManifest.Parse(TestManifest.WithDuplicateHello));

        Assert.Equal("manifest.duplicate_message", error.Code);
    }
}
