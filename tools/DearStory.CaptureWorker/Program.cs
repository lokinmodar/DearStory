namespace DearStory.CaptureWorker;

/// <summary>
/// Provides deterministic placeholder screenshot generation for the Windows docs slice.
/// </summary>
public static class Program
{
    private const string OnePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";

    /// <summary>
    /// Writes a deterministic placeholder PNG to the requested output path.
    /// </summary>
    /// <param name="outputPath">The destination PNG path.</param>
    public static void WriteDeterministicPng(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(outputPath, Convert.FromBase64String(OnePixelPngBase64));
    }

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: DearStory.CaptureWorker <outputPath>");
            return 64;
        }

        WriteDeterministicPng(args[0]);
        return 0;
    }
}
