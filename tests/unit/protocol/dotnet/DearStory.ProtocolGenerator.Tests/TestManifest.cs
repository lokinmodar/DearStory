using System.Text.Json;
using System.Text.Json.Nodes;

namespace DearStory.ProtocolGenerator.Tests;

internal static class TestManifest
{
    internal static string Valid => Load();

    internal static string Load() =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "protocol", "control", "messages.json"));

    internal static ProtocolManifest LoadModel() =>
        ProtocolManifest.Parse(Load());

    internal static string WithDuplicateHello
    {
        get
        {
            var manifest = JsonNode.Parse(Valid)!.AsObject();
            var messages = manifest["messages"]!.AsArray();
            messages.Add(messages[0]!.DeepClone());
            return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
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
