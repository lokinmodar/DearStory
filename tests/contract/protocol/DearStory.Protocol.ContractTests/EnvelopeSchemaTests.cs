using System.Text.Json;
using Json.Schema;
using Xunit;

namespace DearStory.Protocol.ContractTests;

public sealed class EnvelopeSchemaTests
{
    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);
    private static readonly Lazy<JsonSchema> EnvelopeSchema = new(() =>
        JsonSchema.FromText(File.ReadAllText(
            Path.Combine(RepositoryRoot.Value, "protocol", "control", "control-envelope.schema.json"))));

    public static TheoryData<string, bool> Vectors => new()
    {
        { "hello.valid.json", true },
        { "welcome.valid.json", true },
        { "reject.major-mismatch.json", true },
        { "hello.missing-message-id.json", false },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vector_matches_envelope_schema(string fileName, bool expected)
    {
        var root = RepositoryRoot.Value;
        using var instanceDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "protocol", "test-vectors", "handshake", fileName)));

        var result = EnvelopeSchema.Value.Evaluate(instanceDocument.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true,
        });

        Assert.Equal(expected, result.IsValid);
    }

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
