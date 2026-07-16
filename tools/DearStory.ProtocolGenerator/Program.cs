using System.Text;

namespace DearStory.ProtocolGenerator;

internal static class Program
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private static int Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var manifest = ProtocolManifest.Parse(File.ReadAllText(options.ManifestPath, Utf8WithoutBom));
            var models = ModelEmitter.Emit(manifest);

            if (options.Check)
            {
                return CheckOutputs(options, models);
            }

            WriteAtomic(options.CppOutputPath, models.Cpp);
            WriteAtomic(options.CSharpOutputPath, models.CSharp);
            return 0;
        }
        catch (ManifestException exception)
        {
            Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
            return 1;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static GeneratorOptions ParseOptions(IReadOnlyList<string> args)
    {
        string? manifestPath = null;
        string? cppOutputPath = null;
        string? csharpOutputPath = null;
        var check = false;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--manifest":
                    manifestPath = ReadValue(args, ++index, "--manifest");
                    break;
                case "--cpp-output":
                    cppOutputPath = ReadValue(args, ++index, "--cpp-output");
                    break;
                case "--csharp-output":
                    csharpOutputPath = ReadValue(args, ++index, "--csharp-output");
                    break;
                case "--check":
                    check = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(manifestPath) ||
            string.IsNullOrWhiteSpace(cppOutputPath) ||
            string.IsNullOrWhiteSpace(csharpOutputPath))
        {
            throw new ArgumentException("Usage: --manifest <path> --cpp-output <path> --csharp-output <path> [--check]");
        }

        return new GeneratorOptions(manifestPath, cppOutputPath, csharpOutputPath, check);
    }

    private static string ReadValue(IReadOnlyList<string> args, int index, string option)
    {
        if (index >= args.Count)
        {
            throw new ArgumentException($"The option '{option}' requires a value.");
        }

        return args[index];
    }

    private static int CheckOutputs(GeneratorOptions options, GeneratedModels models)
    {
        var differences = new List<string>();
        if (!ContentsMatch(options.CppOutputPath, models.Cpp))
        {
            differences.Add(options.CppOutputPath);
        }

        if (!ContentsMatch(options.CSharpOutputPath, models.CSharp))
        {
            differences.Add(options.CSharpOutputPath);
        }

        if (differences.Count == 0)
        {
            return 0;
        }

        foreach (var difference in differences)
        {
            Console.Error.WriteLine(difference);
        }

        return 2;
    }

    private static bool ContentsMatch(string path, string generatedContent)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var existing = File.ReadAllText(path, Utf8WithoutBom);
        return Normalize(existing) == Normalize(generatedContent);
    }

    private static string Normalize(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException($"The path '{path}' does not contain a directory.");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, Normalize(content), Utf8WithoutBom);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private sealed record GeneratorOptions(
        string ManifestPath,
        string CppOutputPath,
        string CSharpOutputPath,
        bool Check);
}
