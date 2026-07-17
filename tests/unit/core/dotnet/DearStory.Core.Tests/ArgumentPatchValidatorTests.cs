using System.Linq;
using System.Text.Json.Nodes;
using DearStory.Core.Schemas;
using Xunit;

namespace DearStory.Core.Tests;

public sealed class ArgumentPatchValidatorTests
{
    [Fact]
    public void Apply_rejects_unknown_enum_value()
    {
        var schema = ArgumentSchema.Parse(
            """
            {
              "type": "object",
              "properties": {
                "size": {
                  "type": "string",
                  "enum": ["small", "medium", "large"],
                  "x-dearstory-control": "radio"
                }
              }
            }
            """);

        var result = ArgumentPatchValidator.Apply(
            schema,
            JsonNode.Parse("""{"size":"medium"}""")!,
            JsonNode.Parse("""{"size":"giant"}""")!);

        Assert.False(result.Accepted);
        Assert.Equal("args.enum", result.Diagnostics.Single().Code);
        Assert.Equal("medium", result.UpdatedArguments["size"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_rejects_unsupported_schema_keywords()
    {
        var schema = ArgumentSchema.Parse(
            """
            {
              "type": "object",
              "properties": {
                "label": {
                  "type": "string",
                  "pattern": "^[A-Z]+$"
                }
              }
            }
            """);

        var result = ArgumentPatchValidator.Apply(
            schema,
            JsonNode.Parse("""{"label":"Save"}""")!,
            JsonNode.Parse("""{"label":"Discard"}""")!);

        Assert.False(result.Accepted);
        Assert.Equal("args.unsupported_keyword", result.Diagnostics.Single().Code);
        Assert.Equal("Save", result.UpdatedArguments["label"]!.GetValue<string>());
    }
}
