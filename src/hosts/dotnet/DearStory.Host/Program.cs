using System.Reflection;
using System.Runtime.Versioning;

namespace DearStory.Host;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int ExitRuntime = 70;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            var host = new ManagedHost(options.PipeName, options.HostId, Assembly.GetExecutingAssembly());
            await host.RunAsync(CancellationToken.None).ConfigureAwait(false);
            return ExitSuccess;
        }
        catch (UsageException)
        {
            return ExitUsage;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return ExitRuntime;
        }
    }

    private static HostStartupOptions ParseArguments(string[] args)
    {
        string? pipeName = null;
        string? hostId = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            string RequireValue(string name)
            {
                if (index + 1 >= args.Length)
                {
                    throw new UsageException($"Missing value for argument {name}.");
                }

                return args[++index];
            }

            switch (argument)
            {
                case "--pipe":
                    pipeName = RequireValue("--pipe");
                    break;
                case "--host-id":
                    hostId = RequireValue("--host-id");
                    break;
                case "--help":
                case "-h":
                case "/?":
                    throw new UsageException("Usage: DearStory.Host.exe --pipe <name> --host-id <id>");
                default:
                    throw new UsageException($"Unknown argument: {argument}");
            }
        }

        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new UsageException("The --pipe argument is required.");
        }

        if (string.IsNullOrWhiteSpace(hostId))
        {
            throw new UsageException("The --host-id argument is required.");
        }

        return new HostStartupOptions(pipeName, hostId);
    }

    private sealed record HostStartupOptions(string PipeName, string HostId);

    private sealed class UsageException(string message) : Exception(message);
}
