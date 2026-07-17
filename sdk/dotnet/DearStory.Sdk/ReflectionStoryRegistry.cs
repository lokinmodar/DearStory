using System.Reflection;
using System.Text.Json.Nodes;
using DearStory.Core;
using DearStory.Core.Schemas;

namespace DearStory.Sdk;

/// <summary>
/// Builds a DearStory story registry through opt-in runtime reflection.
/// </summary>
public static class ReflectionStoryRegistry
{
    /// <summary>
    /// Builds a DearStory story registry by scanning one assembly through reflection.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="options">The reflection fallback options.</param>
    /// <returns>A generated story registry equivalent built through reflection.</returns>
    /// <exception cref="InvalidOperationException">Reflection fallback is disabled.</exception>
    public static GeneratedStoryRegistry Create(Assembly assembly, ReflectionStoryRegistryOptions options)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.AllowReflectionFallback)
        {
            throw new InvalidOperationException("Reflection fallback is disabled. Use the source-generated registry by default.");
        }

        return ReflectionStoryRegistryBuilder.Build(assembly);
    }

    private static class ReflectionStoryRegistryBuilder
    {
        public static GeneratedStoryRegistry Build(Assembly assembly)
        {
            var registrations = assembly
                .DefinedTypes
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .SelectMany(static type => type.DeclaredMethods)
                .Where(static method => method.GetCustomAttribute<StoryAttribute>() is not null)
                .Select(BuildRegistration)
                .OrderBy(static registration => registration.Descriptor.Id.Value, StringComparer.Ordinal)
                .ToArray();

            return new GeneratedStoryRegistry
            {
                Registrations = registrations,
            };
        }

        private static GeneratedStoryRegistration BuildRegistration(MethodInfo method)
        {
            var storyAttribute = method.GetCustomAttribute<StoryAttribute>()
                ?? throw new InvalidOperationException("The story attribute is required.");

            if (!method.IsStatic)
            {
                throw new InvalidOperationException($"The story method '{method.Name}' must be static.");
            }

            var callback = (StoryCallback)Delegate.CreateDelegate(typeof(StoryCallback), method);
            var descriptor = BuildDescriptor(storyAttribute);
            var arguments = BuildArguments(storyAttribute.ArgsType);
            return new GeneratedStoryRegistration
            {
                Descriptor = descriptor,
                ArgumentSchema = arguments.Schema,
                DefaultArguments = arguments.DefaultArguments,
                Arguments = arguments.Arguments,
                Render = callback,
            };
        }

        private static StoryDescriptor BuildDescriptor(StoryAttribute storyAttribute)
        {
            var rawId = storyAttribute.Id;
            var segments = rawId
                .Trim()
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var titleSegment = segments[^1];
            var title = titleSegment.Length == 0
                ? string.Empty
                : char.ToUpperInvariant(titleSegment[0]) + titleSegment[1..];

            return StoryDescriptor.Create(rawId, title) with
            {
                Hierarchy = segments.Take(Math.Max(segments.Length - 1, 0)).ToArray(),
                Visual = new StoryVisualDescriptor
                {
                    SupportsCapture = true,
                    IncludeInCanonicalCorpus = storyAttribute.IncludeInCanonicalCorpus,
                },
            };
        }

        private static ReflectionArguments BuildArguments(Type argsType)
        {
            var defaultInstance = Activator.CreateInstance(argsType);
            var schemaProperties = new JsonObject();
            var defaultArguments = new JsonObject();
            var required = new JsonArray();
            var arguments = new List<GeneratedStoryArgument>();

            foreach (var property in argsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                var storyArg = property.GetCustomAttribute<StoryArgAttribute>();
                if (storyArg is null)
                {
                    continue;
                }

                var propertySchema = BuildPropertySchema(property.PropertyType, property.GetValue(defaultInstance));
                var defaultValue = ToJsonNode(property.GetValue(defaultInstance));

                schemaProperties[storyArg.Name] = propertySchema.DeepClone();
                defaultArguments[storyArg.Name] = defaultValue?.DeepClone();
                required.Add(storyArg.Name);

                arguments.Add(
                    new GeneratedStoryArgument
                    {
                        Name = storyArg.Name,
                        Schema = propertySchema,
                        DefaultValue = defaultValue,
                    });
            }

            var schemaDocument = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = schemaProperties,
                ["required"] = required,
            };

            return new ReflectionArguments(
                ArgumentSchema.Parse(schemaDocument),
                defaultArguments,
                arguments.ToArray());
        }

        private static JsonObject BuildPropertySchema(Type propertyType, object? defaultValue)
        {
            if (propertyType == typeof(string))
            {
                return new JsonObject
                {
                    ["type"] = "string",
                    ["default"] = defaultValue is string stringValue ? stringValue : string.Empty,
                };
            }

            if (propertyType == typeof(bool))
            {
                return new JsonObject
                {
                    ["type"] = "boolean",
                    ["default"] = defaultValue is bool booleanValue ? booleanValue : false,
                };
            }

            if (propertyType == typeof(int) || propertyType == typeof(long) || propertyType == typeof(short))
            {
                return new JsonObject
                {
                    ["type"] = "integer",
                    ["default"] = defaultValue is null ? 0 : Convert.ToInt64(defaultValue, System.Globalization.CultureInfo.InvariantCulture),
                };
            }

            if (propertyType == typeof(float) || propertyType == typeof(double) || propertyType == typeof(decimal))
            {
                return new JsonObject
                {
                    ["type"] = "number",
                    ["default"] = defaultValue is null ? 0D : Convert.ToDouble(defaultValue, System.Globalization.CultureInfo.InvariantCulture),
                };
            }

            if (propertyType.IsEnum)
            {
                return new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray(propertyType.GetEnumNames().Select(static name => (JsonNode?)name).ToArray()),
                    ["default"] = defaultValue?.ToString() ?? propertyType.GetEnumNames()[0],
                };
            }

            throw new NotSupportedException($"The argument property type '{propertyType.FullName}' is not supported by DearStory reflection fallback.");
        }

        private static JsonNode? ToJsonNode(object? value) => value switch
        {
            null => null,
            string stringValue => JsonValue.Create(stringValue),
            bool booleanValue => JsonValue.Create(booleanValue),
            short shortValue => JsonValue.Create(shortValue),
            int intValue => JsonValue.Create(intValue),
            long longValue => JsonValue.Create(longValue),
            float floatValue => JsonValue.Create(floatValue),
            double doubleValue => JsonValue.Create(doubleValue),
            decimal decimalValue => JsonValue.Create(decimalValue),
            Enum enumValue => JsonValue.Create(enumValue.ToString()),
            _ => throw new NotSupportedException($"The argument default value type '{value.GetType().FullName}' is not supported by DearStory reflection fallback."),
        };

        private sealed record ReflectionArguments(
            ArgumentSchema Schema,
            JsonObject DefaultArguments,
            IReadOnlyList<GeneratedStoryArgument> Arguments);
    }
}
