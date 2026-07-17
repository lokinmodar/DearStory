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

        var output = StoryRegistryGeneratorHarness.Run(source);

        Assert.Contains("buttons/primary", output, StringComparison.Ordinal);
        Assert.Contains("Caption shown on the button.", output, StringComparison.Ordinal);
        Assert.Contains("GeneratedStoryRegistry", output, StringComparison.Ordinal);
    }

    private static class StoryRegistryGeneratorHarness
    {
        public static string Run(string source)
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

            Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            return string.Join(
                Environment.NewLine,
                result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));
        }
    }
}
