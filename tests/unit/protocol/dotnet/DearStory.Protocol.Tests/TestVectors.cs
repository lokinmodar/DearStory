namespace DearStory.Protocol.Tests;

internal static class TestVectors
{
    internal static byte[] ReadBytes(string fileName) =>
        File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), "protocol", "test-vectors", "handshake", fileName));

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
