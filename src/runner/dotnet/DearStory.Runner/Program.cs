using DearStory.Runner.Commands;
using System.Runtime.Versioning;

namespace DearStory.Runner;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private static readonly string HelpText =
        """
        DearStory runner

        Usage:
          dearstory dev <workspacePath>
          dearstory build <workspacePath>
          dearstory --help
        """;

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine(HelpText);
            return (int)RunnerExitCode.Success;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("A workspace path is required.");
            Console.Error.WriteLine(HelpText);
            return (int)RunnerExitCode.ConfigurationFailure;
        }

        try
        {
            return args[0] switch
            {
                "dev" => (int)await new DevCommand().ExecuteAsync(args[1], CancellationToken.None),
                "build" => (int)await new BuildCommand().ExecuteAsync(args[1], CancellationToken.None),
                _ => UnknownCommand(args[0])
            };
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return (int)RunnerExitCode.ConfigurationFailure;
        }
    }

    private static bool IsHelp(string argument)
    {
        return argument is "--help" or "-h" or "help";
    }

    private static int UnknownCommand(string argument)
    {
        Console.Error.WriteLine($"Unknown command '{argument}'.");
        Console.Error.WriteLine(HelpText);
        return (int)RunnerExitCode.ConfigurationFailure;
    }
}
