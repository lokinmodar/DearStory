using DearStory.ProtocolGenerator;
using Xunit;

namespace DearStory.ProtocolGenerator.Tests;

public sealed class ModelEmitterTests
{
    [Fact]
    public void Manifest_contains_story_and_session_messages()
    {
        var manifestJson = TestManifest.Load();
        var manifest = ProtocolManifest.Parse(manifestJson);
        var generated = ModelEmitter.Emit(manifest);

        Assert.Contains("\"name\": \"story_index_published\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"story_session_open\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"argument_patch_result\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"target_snapshot\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("public sealed record StoryIndexPublished", generated.CSharp, StringComparison.Ordinal);
        Assert.Contains("public sealed record StorySessionOpen", generated.CSharp, StringComparison.Ordinal);
        Assert.Contains("public sealed record ArgumentPatchResult", generated.CSharp, StringComparison.Ordinal);
        Assert.Contains("public sealed record TargetSnapshot", generated.CSharp, StringComparison.Ordinal);
    }

    [Fact]
    public void Emit_is_deterministic_and_contains_all_wire_types()
    {
        var manifest = ProtocolManifest.Parse(TestManifest.Valid);

        var first = ModelEmitter.Emit(manifest);
        var second = ModelEmitter.Emit(manifest);

        Assert.Equal(first, second);
        Assert.Contains("struct hello final", first.Cpp, StringComparison.Ordinal);
        Assert.Contains("std::vector<story_descriptor> stories{}", first.Cpp, StringComparison.Ordinal);
        Assert.Contains("bool accepted{}", first.Cpp, StringComparison.Ordinal);
        Assert.Contains("nlohmann::json updatedArguments{}", first.Cpp, StringComparison.Ordinal);
        Assert.Contains("public sealed record Hello", first.CSharp, StringComparison.Ordinal);
        Assert.Contains("public sealed record StoryIndexPublished", first.CSharp, StringComparison.Ordinal);
        Assert.Contains("public required IReadOnlyList<StoryDescriptor> Stories { get; init; }", first.CSharp, StringComparison.Ordinal);
        Assert.Contains("public required bool Accepted { get; init; }", first.CSharp, StringComparison.Ordinal);
        Assert.Contains("public required JsonNode UpdatedArguments { get; init; }", first.CSharp, StringComparison.Ordinal);
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
