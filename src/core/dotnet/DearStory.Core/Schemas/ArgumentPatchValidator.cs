using System.Text.Json;
using System.Text.Json.Nodes;
using DearStory.Protocol.Generated;

namespace DearStory.Core.Schemas;

/// <summary>
/// Validates DearStory argument patches against the canonical schema subset.
/// </summary>
public static class ArgumentPatchValidator
{
    /// <summary>
    /// Applies one patch document to the current arguments and validates the resulting payload.
    /// </summary>
    /// <param name="schema">The parsed DearStory argument schema.</param>
    /// <param name="currentArguments">The current argument snapshot.</param>
    /// <param name="patchDocument">The requested patch document.</param>
    /// <returns>A patch result describing acceptance, the updated arguments, and any diagnostics.</returns>
    public static PatchResult Apply(ArgumentSchema schema, JsonNode currentArguments, JsonNode patchDocument)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(currentArguments);
        ArgumentNullException.ThrowIfNull(patchDocument);

        if (schema.SchemaDiagnostics.Count > 0)
        {
            return new PatchResult
            {
                Accepted = false,
                UpdatedArguments = currentArguments.DeepClone(),
                Diagnostics = schema.SchemaDiagnostics,
            };
        }

        var candidate = ApplyMergePatch(currentArguments, patchDocument);
        var diagnostics = new List<FieldDiagnostic>();
        ValidateNode(schema.Root, candidate, "$", diagnostics);

        return new PatchResult
        {
            Accepted = diagnostics.Count == 0,
            UpdatedArguments = diagnostics.Count == 0 ? candidate : currentArguments.DeepClone(),
            Diagnostics = diagnostics,
        };
    }

    private static JsonNode ApplyMergePatch(JsonNode current, JsonNode patch)
    {
        if (patch is JsonObject patchObject)
        {
            var result = current is JsonObject currentObject
                ? (JsonObject)currentObject.DeepClone()
                : [];

            foreach (var property in patchObject)
            {
                if (property.Value is null)
                {
                    result[property.Key] = null;
                    continue;
                }

                result[property.Key] = result[property.Key] is JsonNode existing
                    ? ApplyMergePatch(existing, property.Value)
                    : property.Value.DeepClone();
            }

            return result;
        }

        return patch.DeepClone();
    }

    private static void ValidateNode(JsonNode schemaNode, JsonNode? value, string fieldPath, List<FieldDiagnostic> diagnostics)
    {
        var schemaObject = schemaNode.AsObject();
        var declaredType = schemaObject["type"] is JsonNode typeNode ? typeNode.GetValue<string>() : null;
        if (declaredType is not null && !MatchesType(declaredType, value))
        {
            diagnostics.Add(
                CreateDiagnostic(
                    "args.type",
                    fieldPath,
                    $"The value at '{fieldPath}' does not match the required '{declaredType}' type.",
                    $"Provide a JSON value whose type is '{declaredType}'."));
            return;
        }

        if (value is null)
        {
            return;
        }

        if (schemaObject["enum"] is JsonArray allowedValues &&
            allowedValues.All(allowedValue => !JsonNode.DeepEquals(allowedValue, value)))
        {
            diagnostics.Add(
                CreateDiagnostic(
                    "args.enum",
                    fieldPath,
                    $"The value at '{fieldPath}' must match one of the declared enumeration values.",
                    "Choose one of the values declared by the schema enumeration."));
            return;
        }

        if (schemaObject["minimum"] is JsonNode minimumNode && TryGetNumber(value, out var numericValue))
        {
            var minimum = minimumNode.GetValue<double>();
            if (numericValue < minimum)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        "args.minimum",
                        fieldPath,
                        $"The value at '{fieldPath}' must be greater than or equal to {minimum}.",
                        "Raise the numeric value to the declared minimum or above."));
            }
        }

        if (schemaObject["maximum"] is JsonNode maximumNode && TryGetNumber(value, out numericValue))
        {
            var maximum = maximumNode.GetValue<double>();
            if (numericValue > maximum)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        "args.maximum",
                        fieldPath,
                        $"The value at '{fieldPath}' must be less than or equal to {maximum}.",
                        "Lower the numeric value to the declared maximum or below."));
            }
        }

        if (schemaObject["minLength"] is JsonNode minLengthNode && TryGetString(value, out var stringValue))
        {
            var minLength = minLengthNode.GetValue<int>();
            if (stringValue.Length < minLength)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        "args.min_length",
                        fieldPath,
                        $"The value at '{fieldPath}' must have at least {minLength} characters.",
                        "Provide a longer string value that satisfies the minimum length."));
            }
        }

        if (schemaObject["maxLength"] is JsonNode maxLengthNode && TryGetString(value, out stringValue))
        {
            var maxLength = maxLengthNode.GetValue<int>();
            if (stringValue.Length > maxLength)
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        "args.max_length",
                        fieldPath,
                        $"The value at '{fieldPath}' must have at most {maxLength} characters.",
                        "Shorten the string value to satisfy the maximum length."));
            }
        }

        if (value is JsonObject objectValue)
        {
            if (schemaObject["required"] is JsonArray requiredProperties)
            {
                foreach (var propertyNode in requiredProperties)
                {
                    var propertyName = propertyNode!.GetValue<string>();
                    if (!objectValue.ContainsKey(propertyName))
                    {
                        diagnostics.Add(
                            CreateDiagnostic(
                                "args.required",
                                AppendPath(fieldPath, propertyName),
                                $"The required property '{propertyName}' is missing at '{fieldPath}'.",
                                $"Add the '{propertyName}' property to the argument payload."));
                    }
                }
            }

            if (schemaObject["properties"] is JsonObject propertySchemas)
            {
                foreach (var propertySchema in propertySchemas)
                {
                    if (propertySchema.Value is null)
                    {
                        continue;
                    }

                    if (objectValue.TryGetPropertyValue(propertySchema.Key, out var propertyValue))
                    {
                        ValidateNode(propertySchema.Value, propertyValue, AppendPath(fieldPath, propertySchema.Key), diagnostics);
                    }
                }
            }
        }

        if (value is JsonArray arrayValue && schemaObject["items"] is JsonNode itemSchema)
        {
            for (var index = 0; index < arrayValue.Count; index++)
            {
                ValidateNode(itemSchema, arrayValue[index], $"{fieldPath}[{index}]", diagnostics);
            }
        }
    }

    private static FieldDiagnostic CreateDiagnostic(string code, string field, string message, string recovery) =>
        new()
        {
            Code = code,
            Field = field,
            Message = message,
            Recovery = recovery,
        };

    private static string AppendPath(string basePath, string propertyName) => $"{basePath}.{propertyName}";

    private static bool MatchesType(string declaredType, JsonNode? value)
    {
        if (value is null)
        {
            return false;
        }

        if (!TryGetElement(value, out var element) &&
            declaredType is "string" or "boolean" or "number" or "integer")
        {
            return false;
        }

        return declaredType switch
        {
            "object" => value is JsonObject,
            "array" => value is JsonArray,
            "string" => element.ValueKind == JsonValueKind.String,
            "boolean" => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && IsInteger(element),
            _ => false,
        };
    }

    private static bool TryGetNumber(JsonNode value, out double numericValue)
    {
        numericValue = default;
        if (!TryGetElement(value, out var element) || element.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        numericValue = element.GetDouble();
        return true;
    }

    private static bool TryGetString(JsonNode value, out string stringValue)
    {
        stringValue = string.Empty;
        if (!TryGetElement(value, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        stringValue = element.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetElement(JsonNode value, out JsonElement element)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        element = document.RootElement.Clone();
        return true;
    }

    private static bool IsInteger(JsonElement element)
    {
        var numericValue = element.GetDouble();
        return Math.Abs(numericValue % 1D) < double.Epsilon;
    }
}

/// <summary>
/// Represents the result of validating and applying one DearStory argument patch.
/// </summary>
public sealed record PatchResult
{
    /// <summary>
    /// Gets a value that indicates whether the patch was accepted.
    /// </summary>
    /// <value><see langword="true" /> if the patch was accepted; otherwise, <see langword="false" />.</value>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Gets the updated argument snapshot.
    /// </summary>
    /// <value>The updated argument snapshot.</value>
    public required JsonNode UpdatedArguments { get; init; }

    /// <summary>
    /// Gets the diagnostics produced while applying the patch.
    /// </summary>
    /// <value>The patch diagnostics.</value>
    public required IReadOnlyList<FieldDiagnostic> Diagnostics { get; init; }
}
