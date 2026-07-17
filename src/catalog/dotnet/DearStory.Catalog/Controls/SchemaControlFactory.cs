using System.Text.Json.Nodes;
using DearStory.Core.Schemas;

namespace DearStory.Catalog.Controls;

/// <summary>
/// Creates schema-driven control descriptors for the Windows catalog shell.
/// </summary>
public sealed class SchemaControlFactory
{
    /// <summary>
    /// Creates a control descriptor for the supplied argument schema.
    /// </summary>
    /// <param name="schema">The validated argument schema that drives the control surface.</param>
    /// <returns>The control descriptor chosen for the schema.</returns>
    public static SchemaControlDescriptor Create(ArgumentSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var document = schema.Document as JsonObject
            ?? throw new InvalidOperationException("The schema root must be a JSON object.");

        var explicitKind = document["x-dearstory-control"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(explicitKind))
        {
            return new SchemaControlDescriptor(explicitKind, document.DeepClone());
        }

        var declaredType = document["type"]?.GetValue<string>();
        var inferredKind = declaredType switch
        {
            "boolean" => "toggle",
            "integer" or "number" => "number",
            "array" => "array",
            "object" => "group",
            _ => "text",
        };

        return new SchemaControlDescriptor(inferredKind, document.DeepClone());
    }

    /// <summary>
    /// Creates a control descriptor through the instance factory surface used by the presenter.
    /// </summary>
    /// <param name="schema">The validated argument schema that drives the control surface.</param>
    /// <returns>The control descriptor chosen for the schema.</returns>
    public SchemaControlDescriptor CreateControl(ArgumentSchema schema) => Create(schema);
}

/// <summary>
/// Describes one schema-driven control chosen for the catalog UI.
/// </summary>
/// <param name="Kind">The stable control kind.</param>
/// <param name="SchemaDocument">The detached schema document that produced the control.</param>
public sealed record SchemaControlDescriptor(string Kind, JsonNode SchemaDocument);
