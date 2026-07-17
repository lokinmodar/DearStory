using System.Text.Json;
using System.Text.Json.Nodes;
using DearStory.Protocol.Generated;

namespace DearStory.Core.Schemas;

/// <summary>
/// Represents one parsed DearStory argument schema document.
/// </summary>
public sealed class ArgumentSchema
{
    private static readonly HashSet<string> SupportedKeywords =
    [
        "type",
        "properties",
        "required",
        "enum",
        "minimum",
        "maximum",
        "minLength",
        "maxLength",
        "items",
        "default",
        "x-dearstory-control",
        "x-dearstory-order",
        "x-dearstory-category",
        "x-dearstory-visible",
    ];

    private static readonly HashSet<string> SupportedTypes =
    [
        "object",
        "boolean",
        "integer",
        "number",
        "string",
        "array",
    ];

    private readonly JsonNode _document;
    private readonly IReadOnlyList<FieldDiagnostic> _schemaDiagnostics;

    private ArgumentSchema(JsonNode document, IReadOnlyList<FieldDiagnostic> schemaDiagnostics)
    {
        _document = document;
        _schemaDiagnostics = schemaDiagnostics;
    }

    /// <summary>
    /// Gets the schema document as a detached JSON node.
    /// </summary>
    /// <value>The schema document.</value>
    public JsonNode Document => _document.DeepClone();

    /// <summary>
    /// Parses one DearStory argument schema JSON document.
    /// </summary>
    /// <param name="json">A JSON document that uses the DearStory argument schema subset.</param>
    /// <returns>A parsed argument schema.</returns>
    /// <exception cref="ArgumentException"><paramref name="json" /> is empty, invalid JSON, or not a valid DearStory schema document.</exception>
    public static ArgumentSchema Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The DearStory argument schema document is not valid JSON.", nameof(json), exception);
        }

        if (parsed is null)
        {
            throw new ArgumentException("The DearStory argument schema document must not be null.", nameof(json));
        }

        return Parse(parsed);
    }

    /// <summary>
    /// Parses one DearStory argument schema JSON node.
    /// </summary>
    /// <param name="document">A JSON node that uses the DearStory argument schema subset.</param>
    /// <returns>A parsed argument schema.</returns>
    /// <exception cref="ArgumentException"><paramref name="document" /> is not a valid DearStory schema document.</exception>
    public static ArgumentSchema Parse(JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var clone = document.DeepClone();
        var diagnostics = new List<FieldDiagnostic>();
        AnalyzeSchemaNode(clone, "$", diagnostics);
        return new ArgumentSchema(clone, diagnostics);
    }

    /// <summary>
    /// Parses one protocol-generated story argument schema document.
    /// </summary>
    /// <param name="schema">A protocol-generated story argument schema.</param>
    /// <returns>A parsed argument schema.</returns>
    public static ArgumentSchema FromProtocol(StoryArgumentSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(schema.Schema);
        return Parse(schema.Schema.DeepClone());
    }

    internal JsonNode Root => _document;

    internal IReadOnlyList<FieldDiagnostic> SchemaDiagnostics => _schemaDiagnostics;

    private static void AnalyzeSchemaNode(JsonNode node, string path, List<FieldDiagnostic> diagnostics)
    {
        if (node is not JsonObject schemaObject)
        {
            throw new ArgumentException($"The schema node at '{path}' must be a JSON object.");
        }

        foreach (var property in schemaObject)
        {
            var keywordPath = $"{path}.{property.Key}";
            switch (property.Key)
            {
                case "type":
                    EnsureTypeKeyword(property.Value, keywordPath);
                    break;
                case "properties":
                    EnsurePropertiesKeyword(property.Value, keywordPath, diagnostics);
                    break;
                case "required":
                    EnsureRequiredKeyword(property.Value, keywordPath);
                    break;
                case "enum":
                    EnsureArrayKeyword(property.Value, keywordPath);
                    break;
                case "minimum":
                case "maximum":
                    EnsureNumberKeyword(property.Value, keywordPath);
                    break;
                case "minLength":
                case "maxLength":
                case "x-dearstory-order":
                    EnsureIntegerKeyword(property.Value, keywordPath);
                    break;
                case "items":
                    EnsureItemsKeyword(property.Value, keywordPath, diagnostics);
                    break;
                case "default":
                    break;
                case "x-dearstory-control":
                case "x-dearstory-category":
                    EnsureStringKeyword(property.Value, keywordPath);
                    break;
                case "x-dearstory-visible":
                    EnsureBooleanKeyword(property.Value, keywordPath);
                    break;
                default:
                    diagnostics.Add(
                        new FieldDiagnostic
                        {
                            Code = "args.unsupported_keyword",
                            Field = keywordPath,
                            Message = $"The schema keyword '{property.Key}' is not supported by the DearStory argument subset.",
                            Recovery = "Remove the unsupported keyword or replace it with a supported DearStory subset keyword.",
                        });
                    break;
            }
        }
    }

    private static void EnsureTypeKeyword(JsonNode? node, string path)
    {
        var value = RequireString(node, path);
        if (!SupportedTypes.Contains(value))
        {
            throw new ArgumentException($"The schema type '{value}' at '{path}' is not supported by DearStory.");
        }
    }

    private static void EnsurePropertiesKeyword(JsonNode? node, string path, List<FieldDiagnostic> diagnostics)
    {
        if (node is not JsonObject properties)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON object.");
        }

        foreach (var property in properties)
        {
            if (property.Value is null)
            {
                throw new ArgumentException($"The schema property '{property.Key}' at '{path}' must not be null.");
            }

            AnalyzeSchemaNode(property.Value, $"{path}.{property.Key}", diagnostics);
        }
    }

    private static void EnsureRequiredKeyword(JsonNode? node, string path)
    {
        if (node is not JsonArray requiredItems)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON array.");
        }

        foreach (var item in requiredItems)
        {
            _ = RequireString(item, path);
        }
    }

    private static void EnsureArrayKeyword(JsonNode? node, string path)
    {
        if (node is not JsonArray)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON array.");
        }
    }

    private static void EnsureNumberKeyword(JsonNode? node, string path)
    {
        var element = RequireElement(node, path);
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON number.");
        }
    }

    private static void EnsureIntegerKeyword(JsonNode? node, string path)
    {
        var element = RequireElement(node, path);
        if (element.ValueKind != JsonValueKind.Number || !IsInteger(element))
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON integer.");
        }
    }

    private static void EnsureItemsKeyword(JsonNode? node, string path, List<FieldDiagnostic> diagnostics)
    {
        if (node is null)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must not be null.");
        }

        AnalyzeSchemaNode(node, path, diagnostics);
    }

    private static void EnsureStringKeyword(JsonNode? node, string path)
    {
        _ = RequireString(node, path);
    }

    private static void EnsureBooleanKeyword(JsonNode? node, string path)
    {
        var element = RequireElement(node, path);
        if (element.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON boolean.");
        }
    }

    private static string RequireString(JsonNode? node, string path)
    {
        var element = RequireElement(node, path);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must be a JSON string.");
        }

        return element.GetString() ?? string.Empty;
    }

    private static JsonElement RequireElement(JsonNode? node, string path)
    {
        if (node is null)
        {
            throw new ArgumentException($"The schema keyword at '{path}' must not be null.");
        }

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static bool IsInteger(JsonElement element)
    {
        var value = element.GetDouble();
        return Math.Abs(value % 1D) < double.Epsilon;
    }
}
