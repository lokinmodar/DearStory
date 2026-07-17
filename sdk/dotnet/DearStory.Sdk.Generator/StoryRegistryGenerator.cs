using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DearStory.Sdk.Generator;

/// <summary>
/// Generates a strongly typed story registry from methods annotated with DearStory story metadata.
/// </summary>
[Generator]
public sealed class StoryRegistryGenerator : IIncrementalGenerator
{
    private const string StoryAttributeName = "DearStory.Sdk.StoryAttribute";
    private const string StoryArgAttributeName = "DearStory.Sdk.StoryArgAttribute";

    private static readonly DiagnosticDescriptor DuplicateStoryIdDiagnostic = new(
        id: "DEARSTORYSDK001",
        title: "Duplicate canonical story id",
        messageFormat: "The story id '{0}' canonicalizes to the duplicate DearStory id '{1}'",
        category: "DearStory",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Registers the discovery and emission pipeline for generated story registry source.
    /// </summary>
    /// <param name="context">The Roslyn generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var stories = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax method && method.AttributeLists.Count > 0,
                static (syntaxContext, cancellationToken) => TransformCandidate(syntaxContext, cancellationToken))
            .Where(static story => story is not null)
            .Select(static (story, _) => story!);

        context.RegisterSourceOutput(stories.Collect(), static (productionContext, storyDefinitions) =>
        {
            Emit(productionContext, storyDefinitions);
        });
    }

    private static StoryDefinition? TransformCandidate(GeneratorSyntaxContext syntaxContext, CancellationToken cancellationToken)
    {
        var methodSyntax = (MethodDeclarationSyntax)syntaxContext.Node;
        if (syntaxContext.SemanticModel.GetDeclaredSymbol(methodSyntax, cancellationToken) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var storyAttribute = methodSymbol.GetAttributes()
            .FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == StoryAttributeName);
        if (storyAttribute is null ||
            storyAttribute.ConstructorArguments.Length < 2 ||
            storyAttribute.ConstructorArguments[0].Value is not string rawId ||
            storyAttribute.ConstructorArguments[1].Value is not INamedTypeSymbol argsType)
        {
            return null;
        }

        var storyArguments = argsType
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(property => CreateArgumentDefinition(property, cancellationToken))
            .Where(static argument => argument is not null)
            .Select(static argument => argument!)
            .OrderBy(static argument => argument.Name, StringComparer.Ordinal)
            .ToImmutableArray();
        var includeInCanonicalCorpus = false;
        foreach (var namedArgument in storyAttribute.NamedArguments)
        {
            if (namedArgument.Key == "IncludeInCanonicalCorpus" &&
                namedArgument.Value.Value is bool value)
            {
                includeInCanonicalCorpus = value;
                break;
            }
        }

        return new StoryDefinition(
            rawId,
            CanonicalizeId(rawId),
            ToDisplayTitle(rawId),
            ToHierarchy(rawId),
            XmlDocumentationReader.ReadSummary(methodSymbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) ?? string.Empty),
            methodSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            includeInCanonicalCorpus,
            storyArguments);
    }

    private static ArgumentDefinition? CreateArgumentDefinition(IPropertySymbol propertySymbol, CancellationToken cancellationToken)
    {
        var attribute = propertySymbol.GetAttributes()
            .FirstOrDefault(static propertyAttribute => propertyAttribute.AttributeClass?.ToDisplayString() == StoryArgAttributeName);
        if (attribute is null ||
            attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Value is not string serializedName)
        {
            return null;
        }

        var summary = XmlDocumentationReader.ReadSummary(propertySymbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) ?? string.Empty);
        var schemaType = ResolveSchemaType(propertySymbol.Type);
        var enumValues = propertySymbol.Type.TypeKind == TypeKind.Enum
            ? propertySymbol.Type.GetMembers().OfType<IFieldSymbol>()
                .Where(static field => field.HasConstantValue)
                .Select(static field => field.Name)
                .ToImmutableArray()
            : ImmutableArray<string>.Empty;
        var defaultLiteral = ReadDefaultLiteral(propertySymbol, cancellationToken);
        var schemaJson = BuildArgumentSchemaJson(schemaType, enumValues, defaultLiteral);

        return new ArgumentDefinition(
            serializedName,
            schemaType,
            schemaJson,
            defaultLiteral,
            summary);
    }

    private static string ResolveSchemaType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String || typeSymbol.TypeKind == TypeKind.Enum)
        {
            return "string";
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            return "boolean";
        }

        if (typeSymbol.SpecialType is SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64)
        {
            return "integer";
        }

        if (typeSymbol.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
        {
            return "number";
        }

        return "string";
    }

    private static string? ReadDefaultLiteral(IPropertySymbol propertySymbol, CancellationToken cancellationToken)
    {
        var syntax = propertySymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault();

        if (syntax?.Initializer?.Value is null)
        {
            return null;
        }

        return syntax.Initializer.Value switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression) =>
                literal.Token.ValueText,
            LiteralExpressionSyntax literal when literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression) =>
                "true",
            LiteralExpressionSyntax literal when literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.FalseLiteralExpression) =>
                "false",
            LiteralExpressionSyntax literal when literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression) =>
                literal.Token.Value?.ToString(),
            _ => null,
        };
    }

    private static string BuildArgumentSchemaJson(string schemaType, ImmutableArray<string> enumValues, string? defaultLiteral)
    {
        var builder = new StringBuilder();
        builder.Append("{\"type\":\"").Append(schemaType).Append('"');

        if (!enumValues.IsDefaultOrEmpty)
        {
            builder.Append(",\"enum\":[");
            for (var index = 0; index < enumValues.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append('"').Append(EscapeJson(enumValues[index])).Append('"');
            }

            builder.Append(']');
        }

        if (defaultLiteral is not null)
        {
            builder.Append(",\"default\":").Append(ToJsonLiteral(schemaType, defaultLiteral));
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<StoryDefinition> stories)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var story in stories.OrderBy(static story => story.CanonicalId, StringComparer.Ordinal))
        {
            if (seen.TryGetValue(story.CanonicalId, out var existing))
            {
                context.ReportDiagnostic(Diagnostic.Create(DuplicateStoryIdDiagnostic, Location.None, story.RawId, existing));
                continue;
            }

            seen.Add(story.CanonicalId, story.CanonicalId);
        }

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("namespace DearStory.Sdk;");
        builder.AppendLine();
        builder.AppendLine("public sealed partial class GeneratedStoryRegistry");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>Creates the source-generated DearStory story registry.</summary>");
        builder.AppendLine("    public static GeneratedStoryRegistry Create()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new GeneratedStoryRegistry");
        builder.AppendLine("        {");
        builder.AppendLine("            Registrations = new GeneratedStoryRegistration[]");
        builder.AppendLine("            {");

        foreach (var story in stories.OrderBy(static story => story.CanonicalId, StringComparer.Ordinal))
        {
            builder.AppendLine("                new()");
            builder.AppendLine("                {");
            builder.Append("                    Descriptor = global::DearStory.Core.StoryDescriptor.Create(\"")
                .Append(EscapeCSharp(story.RawId))
                .Append("\", \"")
                .Append(EscapeCSharp(story.Title))
                .AppendLine("\") with");
            builder.AppendLine("                    {");
            builder.AppendLine("                        Hierarchy = new string[]");
            builder.AppendLine("                        {");
            foreach (var segment in story.Hierarchy)
            {
                builder.Append("                            \"")
                    .Append(EscapeCSharp(segment))
                    .AppendLine("\",");
            }

            builder.AppendLine("                        },");
            if (story.StoryDescription is not null)
            {
                builder.Append("                        Description = \"")
                    .Append(EscapeCSharp(story.StoryDescription))
                    .AppendLine("\",");
            }

            builder.AppendLine("                        Visual = new global::DearStory.Core.StoryVisualDescriptor");
            builder.AppendLine("                        {");
            builder.AppendLine("                            SupportsCapture = true,");
            builder.Append("                            IncludeInCanonicalCorpus = ")
                .Append(story.IncludeInCanonicalCorpus ? "true" : "false")
                .AppendLine(",");
            builder.AppendLine("                        },");
            builder.AppendLine("                    },");
            builder.Append("                    ArgumentSchema = global::DearStory.Core.Schemas.ArgumentSchema.Parse(\"")
                .Append(EscapeCSharp(BuildStorySchemaJson(story)))
                .AppendLine("\"),");
            builder.Append("                    DefaultArguments = global::System.Text.Json.Nodes.JsonNode.Parse(\"")
                .Append(EscapeCSharp(BuildDefaultArgumentsJson(story)))
                .AppendLine("\")!,");
            builder.AppendLine("                    Arguments = new GeneratedStoryArgument[]");
            builder.AppendLine("                    {");
            foreach (var argument in story.Arguments)
            {
                builder.AppendLine("                        new()");
                builder.AppendLine("                        {");
                builder.Append("                            Name = \"")
                    .Append(EscapeCSharp(argument.Name))
                    .AppendLine("\",");
                builder.Append("                            Schema = global::System.Text.Json.Nodes.JsonNode.Parse(\"")
                    .Append(EscapeCSharp(argument.SchemaJson))
                    .AppendLine("\")!,");
                if (argument.DefaultLiteral is not null)
                {
                    builder.Append("                            DefaultValue = ")
                        .Append(ToJsonNodeFactory(argument.SchemaType, argument.DefaultLiteral))
                        .AppendLine(",");
                }

                if (argument.Description is not null)
                {
                    builder.Append("                            Description = \"")
                        .Append(EscapeCSharp(argument.Description))
                        .AppendLine("\",");
                }

                builder.AppendLine("                        },");
            }

            builder.AppendLine("                    },");
            builder.Append("                    Render = ")
                .Append(story.CallbackExpression)
                .AppendLine(",");
            builder.AppendLine("                },");
        }

        builder.AppendLine("            },");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource("GeneratedStoryRegistry.g.cs", builder.ToString());
    }

    private static string BuildStorySchemaJson(StoryDefinition story)
    {
        var builder = new StringBuilder();
        builder.Append("{\"type\":\"object\",\"properties\":{");
        for (var index = 0; index < story.Arguments.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var argument = story.Arguments[index];
            builder.Append('"')
                .Append(EscapeJson(argument.Name))
                .Append("\":")
                .Append(argument.SchemaJson);
        }

        builder.Append("},\"required\":[");
        for (var index = 0; index < story.Arguments.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(EscapeJson(story.Arguments[index].Name)).Append('"');
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildDefaultArgumentsJson(StoryDefinition story)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        for (var index = 0; index < story.Arguments.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var argument = story.Arguments[index];
            builder.Append('"')
                .Append(EscapeJson(argument.Name))
                .Append("\":")
                .Append(argument.DefaultLiteral is null ? "null" : ToJsonLiteral(argument.SchemaType, argument.DefaultLiteral));
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string ToJsonNodeFactory(string schemaType, string defaultLiteral) => schemaType switch
    {
        "string" => "global::System.Text.Json.Nodes.JsonValue.Create(\"" + EscapeCSharp(defaultLiteral) + "\")!",
        "boolean" => "global::System.Text.Json.Nodes.JsonValue.Create(" + defaultLiteral.ToLowerInvariant() + ")!",
        "integer" => "global::System.Text.Json.Nodes.JsonValue.Create(" + defaultLiteral + ")!",
        "number" => "global::System.Text.Json.Nodes.JsonValue.Create(" + defaultLiteral + ")!",
        _ => "global::System.Text.Json.Nodes.JsonValue.Create(\"" + EscapeCSharp(defaultLiteral) + "\")!",
    };

    private static string ToJsonLiteral(string schemaType, string defaultLiteral) => schemaType switch
    {
        "string" => "\"" + EscapeJson(defaultLiteral) + "\"",
        "boolean" => defaultLiteral.ToLowerInvariant(),
        "integer" => defaultLiteral,
        "number" => defaultLiteral,
        _ => "\"" + EscapeJson(defaultLiteral) + "\"",
    };

    private static string CanonicalizeId(string rawId)
    {
        var segments = SplitStorySegments(rawId)
            .Select(static segment => segment.ToLowerInvariant())
            .ToArray();

        return string.Join("/", segments);
    }

    private static string ToDisplayTitle(string rawId)
    {
        var segments = SplitStorySegments(rawId);
        var titleSegment = segments[segments.Length - 1];
        return titleSegment.Length == 0
            ? string.Empty
            : char.ToUpperInvariant(titleSegment[0]) + titleSegment.Substring(1);
    }

    private static ImmutableArray<string> ToHierarchy(string rawId)
    {
        var segments = SplitStorySegments(rawId);

        return segments.Take(Math.Max(segments.Length - 1, 0)).ToImmutableArray();
    }

    private static string[] SplitStorySegments(string rawId)
    {
        return rawId
            .Trim()
            .Replace('\\', '/')
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment => segment.Trim())
            .Where(static segment => segment.Length > 0)
            .ToArray();
    }

    private static string EscapeJson(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class StoryDefinition
    {
        public StoryDefinition(
            string rawId,
            string canonicalId,
            string title,
            ImmutableArray<string> hierarchy,
            string? storyDescription,
            string callbackExpression,
            bool includeInCanonicalCorpus,
            ImmutableArray<ArgumentDefinition> arguments)
        {
            RawId = rawId;
            CanonicalId = canonicalId;
            Title = title;
            Hierarchy = hierarchy;
            StoryDescription = storyDescription;
            CallbackExpression = callbackExpression;
            IncludeInCanonicalCorpus = includeInCanonicalCorpus;
            Arguments = arguments;
        }

        public string RawId { get; }

        public string CanonicalId { get; }

        public string Title { get; }

        public ImmutableArray<string> Hierarchy { get; }

        public string? StoryDescription { get; }

        public string CallbackExpression { get; }

        public bool IncludeInCanonicalCorpus { get; }

        public ImmutableArray<ArgumentDefinition> Arguments { get; }
    }

    private sealed class ArgumentDefinition
    {
        public ArgumentDefinition(
            string name,
            string schemaType,
            string schemaJson,
            string? defaultLiteral,
            string? description)
        {
            Name = name;
            SchemaType = schemaType;
            SchemaJson = schemaJson;
            DefaultLiteral = defaultLiteral;
            Description = description;
        }

        public string Name { get; }

        public string SchemaType { get; }

        public string SchemaJson { get; }

        public string? DefaultLiteral { get; }

        public string? Description { get; }
    }
}
