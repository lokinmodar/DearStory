using System.Text.Json.Nodes;
using DearStory.Core.Schemas;
using DearStory.Protocol.Generated;
using Xunit;

namespace DearStory.Core.Tests;

public sealed class ArgumentSchemaTests
{
    [Fact]
    public void Parse_string_rejects_invalid_json_and_empty_input()
    {
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse(string.Empty));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("{"));
    }

    [Fact]
    public void Parse_rejects_invalid_keyword_shapes_and_types()
    {
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"type":["object"]}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"type":"date"}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"properties":[]}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"required":{}}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"enum":"large"}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"minimum":"low"}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"minLength":1.5}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"items":null}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"x-dearstory-control":5}"""));
        Assert.Throws<ArgumentException>(() => ArgumentSchema.Parse("""{"x-dearstory-visible":"yes"}"""));
    }

    [Fact]
    public void From_protocol_clones_the_schema_document()
    {
        var protocolSchema = new StoryArgumentSchema
        {
            Dialect = "dearstory.argument-schema/v1",
            Schema = JsonNode.Parse("""{"type":"object","properties":{"label":{"type":"string"}}}""")!,
        };

        var schema = ArgumentSchema.FromProtocol(protocolSchema);
        var document = schema.Document.AsObject();
        document["type"] = "array";

        Assert.Equal("object", schema.Document["type"]!.GetValue<string>());
        Assert.Equal("string", schema.Document["properties"]!["label"]!["type"]!.GetValue<string>());
    }
}
