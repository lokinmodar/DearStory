namespace DearStory.WindowsSlice.E2ETests;

internal sealed class VisualArtifactEnvironment : IDisposable
{
    private readonly string? _previousValue = Environment.GetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT");

    public VisualArtifactEnvironment()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "dearstory-visual-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        Environment.SetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT", RootPath);
    }

    public string RootPath { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DEARSTORY_VISUAL_ARTIFACT_ROOT", _previousValue);
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
