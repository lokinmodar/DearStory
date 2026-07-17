using System.Collections.Immutable;
using System.Reflection;
using DearStory.Sdk;
using DearStory.Sdk.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DearStory.Sdk.Generator.Tests;

public sealed class StoryRegistryGeneratorTests
{
    [Fact]
    public void Generator_emits_descriptor_with_xml_documentation_and_args()
    {
        const string source =
            """
            using DearStory.Sdk;

            /// <summary>Primary button story.</summary>
            public static class Stories
            {
                [Story("buttons/primary", typeof(PrimaryButtonArgs))]
                public static void PrimaryButton(StoryContext context) {}
            }

            public sealed class PrimaryButtonArgs
            {
                /// <summary>Caption shown on the button.</summary>
                [StoryArg("label")]
                public string Label { get; init; } = "Save";
            }
            """;

        var result = StoryRegistryGeneratorHarness.Run(source);

        Assert.Contains("buttons/primary", result.Output, StringComparison.Ordinal);
        Assert.Contains("Caption shown on the button.", result.Output, StringComparison.Ordinal);
        Assert.Contains("GeneratedStoryRegistry", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_emits_schema_defaults_for_supported_types()
    {
        const string source =
            """
            using DearStory.Sdk;

            public enum ButtonTone
            {
                Primary,
                Secondary,
            }

            public static class Stories
            {
                [Story("Buttons\\Primary", typeof(PrimaryButtonArgs))]
                public static void PrimaryButton(StoryContext context) {}
            }

            public sealed class PrimaryButtonArgs
            {
                [StoryArg("disabled")]
                public bool Disabled { get; init; } = true;

                [StoryArg("count")]
                public int Count { get; init; } = 3;

                [StoryArg("ratio")]
                public double Ratio { get; init; } = 1.5;

                [StoryArg("tone")]
                public ButtonTone Tone { get; init; } = ButtonTone.Secondary;
            }
            """;

        var result = StoryRegistryGeneratorHarness.Run(source);

        Assert.Contains("Name = \"disabled\"", result.Output, StringComparison.Ordinal);
        Assert.Contains("JsonValue.Create(true)", result.Output, StringComparison.Ordinal);
        Assert.Contains("Name = \"count\"", result.Output, StringComparison.Ordinal);
        Assert.Contains("JsonValue.Create(3)", result.Output, StringComparison.Ordinal);
        Assert.Contains("Name = \"ratio\"", result.Output, StringComparison.Ordinal);
        Assert.Contains("Name = \"tone\"", result.Output, StringComparison.Ordinal);
        Assert.Contains("\\\"enum\\\":[\\\"Primary\\\",\\\"Secondary\\\"]", result.Output, StringComparison.Ordinal);
        Assert.Contains(@"""Buttons""", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("DEARSTORYSDK001", result.Diagnostics.Select(static diagnostic => diagnostic.Id));
    }

    [Fact]
    public void Generator_emits_visual_metadata_for_canonical_corpus_stories()
    {
        const string source =
            """
            using DearStory.Sdk;

            public static class Stories
            {
                [Story("buttons/primarymanaged", typeof(PrimaryButtonArgs), IncludeInCanonicalCorpus = true)]
                public static void PrimaryButton(StoryContext context) {}
            }

            public sealed class PrimaryButtonArgs
            {
                [StoryArg("label")]
                public string Label { get; init; } = "Save";
            }
            """;

        var result = StoryRegistryGeneratorHarness.Run(source);

        Assert.Contains("Visual = new global::DearStory.Core.StoryVisualDescriptor", result.Output, StringComparison.Ordinal);
        Assert.Contains("SupportsCapture = true", result.Output, StringComparison.Ordinal);
        Assert.Contains("IncludeInCanonicalCorpus = true", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_reports_duplicate_canonical_story_ids()
    {
        const string source =
            """
            using DearStory.Sdk;

            public static class Stories
            {
                [Story("buttons/primary", typeof(PrimaryButtonArgs))]
                public static void PrimaryButton(StoryContext context) {}

                [Story("Buttons\\Primary", typeof(PrimaryButtonArgs))]
                public static void DuplicatePrimaryButton(StoryContext context) {}
            }

            public sealed class PrimaryButtonArgs
            {
                [StoryArg("label")]
                public string Label { get; init; } = "Save";
            }
            """;

        var result = StoryRegistryGeneratorHarness.Run(source, allowGeneratorErrors: true);

        var duplicateDiagnostic = Assert.Single(result.Diagnostics.Where(static diagnostic => diagnostic.Id == "DEARSTORYSDK001"));
        Assert.Equal(DiagnosticSeverity.Error, duplicateDiagnostic.Severity);
    }

    private static class StoryRegistryGeneratorHarness
    {
        public static GeneratorExecutionResult Run(string source, bool allowGeneratorErrors = false)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose));

            var loadedReferences = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
                .Cast<MetadataReference>();

            var explicitReferences = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(StoryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(StoryContext).Assembly.Location),
            };

            var references = loadedReferences
                .Concat(explicitReferences)
                .GroupBy(static reference => ((PortableExecutableReference)reference).FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToImmutableArray();

            var compilation = CSharpCompilation.Create(
                assemblyName: "DearStory.Sdk.Generator.Tests.Generated",
                syntaxTrees: [syntaxTree],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            Assert.Empty(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

            IIncrementalGenerator generator = new StoryRegistryGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult();

            if (!allowGeneratorErrors) {
                Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            }
            return new GeneratorExecutionResult(
                string.Join(
                    Environment.NewLine,
                    result.GeneratedTrees.Select(static tree => tree.GetText().ToString())),
                result.Diagnostics);
        }
    }

    private sealed record GeneratorExecutionResult(string Output, ImmutableArray<Diagnostic> Diagnostics);
}
