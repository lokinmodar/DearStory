using System.Text.Json;
using System.Text.RegularExpressions;

namespace DearStory.ProtocolGenerator;

/// <summary>Represents the parsed DearStory protocol manifest.</summary>
public sealed class ProtocolManifest
{
    private static readonly Regex IdentifierPattern =
        new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private ProtocolManifest(
        ProtocolVersionDefinition protocol,
        IReadOnlyList<ManifestEnumDefinition> enums,
        IReadOnlyList<ManifestRecordDefinition> records,
        IReadOnlyList<ManifestMessageDefinition> messages)
    {
        Protocol = protocol;
        Enums = enums;
        Records = records;
        Messages = messages;
    }

    internal ProtocolVersionDefinition Protocol { get; }

    internal IReadOnlyList<ManifestEnumDefinition> Enums { get; }

    internal IReadOnlyList<ManifestRecordDefinition> Records { get; }

    internal IReadOnlyList<ManifestMessageDefinition> Messages { get; }

    /// <summary>Parses a JSON manifest into a validated protocol model.</summary>
    /// <param name="json">The manifest JSON text to parse.</param>
    /// <returns>A validated manifest model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json" /> is <see langword="null" />.</exception>
    /// <exception cref="ManifestException">The manifest is invalid.</exception>
    public static ProtocolManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw InvalidJson("The manifest root must be an object.");
            }

            EnsureAllowedProperties(root, ["protocol", "enums", "records", "messages"], "manifest");

            var protocol = ParseProtocol(GetRequiredProperty(root, "protocol"));
            var enums = ParseEnums(GetRequiredProperty(root, "enums"));
            var records = ParseRecords(GetRequiredProperty(root, "records"));
            var messages = ParseMessages(GetRequiredProperty(root, "messages"));

            var knownTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "string",
                "uint16",
                "uuid",
                "object",
                "boolean",
                "json"
            };

            foreach (var enumDefinition in enums)
            {
                knownTypes.Add(enumDefinition.Name);
            }

            foreach (var record in records)
            {
                knownTypes.Add(record.Name);
            }

            foreach (var message in messages)
            {
                knownTypes.Add(message.Name);
            }

            ValidateKnownTypes(records.SelectMany(static record => record.Fields), knownTypes);
            ValidateKnownTypes(messages.SelectMany(static message => message.Fields), knownTypes);

            return new ProtocolManifest(protocol, enums, records, messages);
        }
        catch (JsonException exception)
        {
            throw new ManifestException("manifest.invalid_json", "The manifest JSON could not be parsed.", exception);
        }
    }

    private static ProtocolVersionDefinition ParseProtocol(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw InvalidJson("The protocol node must be an object.");
        }

        EnsureAllowedProperties(element, ["major", "minor"], "protocol");
        return new ProtocolVersionDefinition(
            ReadUInt16(GetRequiredProperty(element, "major"), "protocol.major"),
            ReadUInt16(GetRequiredProperty(element, "minor"), "protocol.minor"));
    }

    private static IReadOnlyList<ManifestEnumDefinition> ParseEnums(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidJson("The enums node must be an array.");
        }

        var results = new List<ManifestEnumDefinition>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw InvalidJson("Each enum node must be an object.");
            }

            EnsureAllowedProperties(item, ["name", "values"], "enum");
            var name = ReadIdentifier(GetRequiredProperty(item, "name"), "enum.name");
            if (!names.Add(name))
            {
                throw InvalidJson($"The enum '{name}' is declared more than once.");
            }

            var valuesNode = GetRequiredProperty(item, "values");
            if (valuesNode.ValueKind != JsonValueKind.Array)
            {
                throw InvalidJson($"The enum '{name}' values must be an array.");
            }

            var values = new List<string>();
            var seenValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var valueNode in valuesNode.EnumerateArray())
            {
                var value = ReadIdentifier(valueNode, $"{name}.values");
                if (!seenValues.Add(value))
                {
                    throw InvalidJson($"The enum '{name}' contains the duplicate value '{value}'.");
                }

                values.Add(value);
            }

            results.Add(new ManifestEnumDefinition(name, values));
        }

        return results;
    }

    private static IReadOnlyList<ManifestRecordDefinition> ParseRecords(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidJson("The records node must be an array.");
        }

        var results = new List<ManifestRecordDefinition>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw InvalidJson("Each record node must be an object.");
            }

            EnsureAllowedProperties(item, ["name", "fields"], "record");
            var name = ReadIdentifier(GetRequiredProperty(item, "name"), "record.name");
            if (!names.Add(name))
            {
                throw InvalidJson($"The record '{name}' is declared more than once.");
            }

            results.Add(new ManifestRecordDefinition(name, ParseFields(name, GetRequiredProperty(item, "fields"))));
        }

        return results;
    }

    private static IReadOnlyList<ManifestMessageDefinition> ParseMessages(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidJson("The messages node must be an array.");
        }

        var results = new List<ManifestMessageDefinition>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw InvalidJson("Each message node must be an object.");
            }

            EnsureAllowedProperties(item, ["name", "fields"], "message");
            var name = ReadIdentifier(GetRequiredProperty(item, "name"), "message.name");
            if (!names.Add(name))
            {
                throw new ManifestException("manifest.duplicate_message", $"The message '{name}' is declared more than once.");
            }

            results.Add(new ManifestMessageDefinition(name, ParseFields(name, GetRequiredProperty(item, "fields"))));
        }

        return results;
    }

    private static IReadOnlyList<ManifestFieldDefinition> ParseFields(string ownerName, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw InvalidJson($"The '{ownerName}' fields node must be an array.");
        }

        var fields = new List<ManifestFieldDefinition>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw InvalidJson($"Each '{ownerName}' field must be an object.");
            }

            EnsureAllowedProperties(item, ["name", "type", "required"], $"{ownerName}.field");
            var name = ReadIdentifier(GetRequiredProperty(item, "name"), $"{ownerName}.field.name");
            if (!names.Add(name))
            {
                throw new ManifestException("manifest.duplicate_field", $"The '{ownerName}' shape declares the field '{name}' more than once.");
            }

            var type = ReadIdentifier(GetRequiredProperty(item, "type"), $"{ownerName}.{name}.type", allowArraySuffix: true);
            var required = ReadBoolean(GetRequiredProperty(item, "required"), $"{ownerName}.{name}.required");
            fields.Add(new ManifestFieldDefinition(name, type, required));
        }

        return fields;
    }

    private static void ValidateKnownTypes(IEnumerable<ManifestFieldDefinition> fields, ISet<string> knownTypes)
    {
        foreach (var field in fields)
        {
            var typeName = field.Type.EndsWith("[]", StringComparison.Ordinal)
                ? field.Type[..^2]
                : field.Type;

            if (!knownTypes.Contains(typeName))
            {
                throw new ManifestException("manifest.unknown_type", $"The manifest references the unsupported field type '{field.Type}'.");
            }
        }
    }

    private static void EnsureAllowedProperties(JsonElement element, IReadOnlyCollection<string> allowed, string scope)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new ManifestException("manifest.unknown_property", $"The property '{property.Name}' is not supported in {scope}.");
            }
        }
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            throw InvalidJson($"The required property '{name}' is missing.");
        }

        return property;
    }

    private static string ReadIdentifier(JsonElement element, string path, bool allowArraySuffix = false)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw InvalidJson($"The '{path}' value must be a string.");
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ManifestException("manifest.invalid_identifier", $"The '{path}' identifier cannot be empty.");
        }

        if (allowArraySuffix && value.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementName = value[..^2];
            if (IdentifierPattern.IsMatch(elementName))
            {
                return value;
            }
        }

        if (!IdentifierPattern.IsMatch(value))
        {
            throw new ManifestException("manifest.invalid_identifier", $"The '{value}' identifier from '{path}' is invalid.");
        }

        return value;
    }

    private static ushort ReadUInt16(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetUInt16(out var value))
        {
            throw InvalidJson($"The '{path}' value must be a UInt16.");
        }

        return value;
    }

    private static bool ReadBoolean(JsonElement element, string path)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw InvalidJson($"The '{path}' value must be a Boolean.")
        };
    }

    private static ManifestException InvalidJson(string message) => new("manifest.invalid_json", message);
}

internal sealed record ProtocolVersionDefinition(ushort Major, ushort Minor);

internal sealed record ManifestEnumDefinition(string Name, IReadOnlyList<string> Values);

internal sealed record ManifestRecordDefinition(string Name, IReadOnlyList<ManifestFieldDefinition> Fields);

internal sealed record ManifestMessageDefinition(string Name, IReadOnlyList<ManifestFieldDefinition> Fields);

internal sealed record ManifestFieldDefinition(string Name, string Type, bool Required);
