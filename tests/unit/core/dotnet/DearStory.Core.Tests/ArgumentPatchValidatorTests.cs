using System.Linq;
using System.Text.Json.Nodes;
using DearStory.Core.Schemas;
using Xunit;

namespace DearStory.Core.Tests;

public sealed class ArgumentPatchValidatorTests
{
    [Fact]
    public void Apply_accepts_valid_nested_merge_patch()
    {
        var schema = ArgumentSchema.Parse(
            """
            {
              "type": "object",
              "properties": {
                "appearance": {
                  "type": "object",
                  "properties": {
                    "label": { "type": "string", "minLength": 2, "maxLength": 10 },
                    "disabled": { "type": "boolean" }
                  },
                  "required": ["label", "disabled"]
                }
              },
              "required": ["appearance"]
            }
            """);

        var result = ArgumentPatchValidator.Apply(
            schema,
            JsonNode.Parse("""{"appearance":{"label":"Save","disabled":false}}""")!,
            JsonNode.Parse("""{"appearance":{"disabled":true}}""")!);

        Assert.True(result.Accepted);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("Save", result.UpdatedArguments["appearance"]!["label"]!.GetValue<string>());
        Assert.True(result.UpdatedArguments["appearance"]!["disabled"]!.GetValue<bool>());
    }

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

    [Fact]
    public void Apply_rejects_numeric_and_string_constraint_violations()
    {
        var schema = ArgumentSchema.Parse(
            """
            {
              "type": "object",
              "properties": {
                "count": { "type": "integer", "minimum": 1, "maximum": 5 },
                "label": { "type": "string", "minLength": 2, "maxLength": 4 }
              },
              "required": ["count", "label"]
            }
            """);

        var result = ArgumentPatchValidator.Apply(
            schema,
            JsonNode.Parse("""{"count":3,"label":"Save"}""")!,
            JsonNode.Parse("""{"count":0,"label":"TooLong"}""")!);

        Assert.False(result.Accepted);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "args.minimum");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "args.max_length");
        Assert.Equal(3, result.UpdatedArguments["count"]!.GetValue<int>());
        Assert.Equal("Save", result.UpdatedArguments["label"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_rejects_missing_required_properties_and_invalid_array_items()
    {
        var schema = ArgumentSchema.Parse(
            """
            {
              "type": "object",
              "properties": {
                "appearance": {
                  "type": "object",
                  "properties": {
                    "label": { "type": "string" }
                  },
                  "required": ["label"]
                },
                "steps": {
                  "type": "array",
                  "items": {
                    "type": "integer"
                  }
                }
              },
              "required": ["appearance", "steps"]
            }
            """);

        var result = ArgumentPatchValidator.Apply(
            schema,
            JsonNode.Parse("""{"appearance":{},"steps":[1,2]}""")!,
            JsonNode.Parse("""{"steps":[1,"bad"]}""")!);

        Assert.False(result.Accepted);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Field == "$.appearance.label" && diagnostic.Code == "args.required");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Field == "$.steps[1]" && diagnostic.Code == "args.type");
    }

    [Fact]
    public void Apply_rejects_type_mismatch_and_preserves_current_arguments()
    {
        var schema = ArgumentSchema.Parse(
            """
            {
              "type": "object",
              "properties": {
                "enabled": { "type": "boolean" },
                "title": { "type": "string" }
              },
              "required": ["enabled", "title"]
            }
            """);

        var result = ArgumentPatchValidator.Apply(
            schema,
            JsonNode.Parse("""{"enabled":true,"title":"Save"}""")!,
            JsonNode.Parse("""{"enabled":"yes"}""")!);

        Assert.False(result.Accepted);
        Assert.Equal("args.type", result.Diagnostics.Single().Code);
        Assert.True(result.UpdatedArguments["enabled"]!.GetValue<bool>());
    }
}
