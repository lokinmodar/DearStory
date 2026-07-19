using System.Diagnostics;
using DearStory.Testing;

namespace DearStory.WindowsSlice.E2ETests;

internal static class DearStoryCommand
{
    public static async Task<DearStoryCommandResult> RunAsync(params string[] arguments)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var artifactsDocsDirectory = Path.Combine(repositoryRoot, "artifacts", "docs");
        if (Directory.Exists(artifactsDocsDirectory))
        {
            Directory.Delete(artifactsDocsDirectory, recursive: true);
        }

        Directory.CreateDirectory(artifactsDocsDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(CurrentBuildConfiguration.CurrentConfiguration());
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "src", "runner", "dotnet", "DearStory.Runner", "DearStory.Runner.csproj"));
        startInfo.ArgumentList.Add("--");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The DearStory runner process could not be started.");

        var standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        var outputFiles = Directory.Exists(artifactsDocsDirectory)
            ? Directory.GetFiles(artifactsDocsDirectory, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
                .Cast<string>()
                .OrderBy(static fileName => fileName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        return new DearStoryCommandResult(process.ExitCode, standardOutput, standardError, outputFiles);
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "runner", "dotnet", "DearStory.Runner", "DearStory.Runner.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The DearStory repository root could not be resolved from the e2e test base directory.");
    }
}

internal sealed record DearStoryCommandResult(int ExitCode, string StandardOutput, string StandardError, IReadOnlyList<string> OutputFiles);
