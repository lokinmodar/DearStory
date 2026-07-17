using System.Text.Json.Nodes;
using DearStory.Core.Schemas;
using Xunit;

namespace DearStory.Core.ContractTests;

public sealed class SchemaVectorTests
{
    public static TheoryData<string> Vectors => new()
    {
        "patch-valid.string.json",
        "patch-invalid.enum.json",
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Shared_patch_vector_matches_expected_result(string fileName)
    {
        var vector = JsonNode.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot.Value, "tests", "contract", "core", "vectors", fileName)))!.AsObject();

        var schema = ArgumentSchema.Parse(vector["schema"]!.ToJsonString());
        var result = ArgumentPatchValidator.Apply(
            schema,
            vector["currentArguments"]!.DeepClone(),
            vector["patch"]!.DeepClone());

        var expected = vector["expected"]!.AsObject();
        var expectedAccepted = expected["accepted"]!.GetValue<bool>();
        var expectedUpdatedArguments = expected["updatedArguments"]!;
        var expectedDiagnosticCodes = expected["diagnosticCodes"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

        Assert.Equal(expectedAccepted, result.Accepted);
        Assert.True(JsonNode.DeepEquals(expectedUpdatedArguments, result.UpdatedArguments));
        Assert.Equal(expectedDiagnosticCodes, result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root containing DearStory.slnx was not found.");
    }
}
