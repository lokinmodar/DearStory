using System.Text.Json;
using DearStory.Protocol;
using DearStory.Protocol.Generated;
using DearStory.Transport.Windows;
using GeneratedProtocolVersion = DearStory.Protocol.Generated.ProtocolVersion;
using WireProtocolVersion = DearStory.Protocol.ProtocolVersion;

namespace DearStory.ProtocolProbe.DotNet;

internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 20;
    private const int ExitPipe = 21;
    private const int ExitProtocol = 22;
    private const int ExitTimeout = 23;

    private sealed class UsageException(string message) : Exception(message);

    private sealed class Options
    {
        public required string Mode { get; init; }
        public required string Pipe { get; set; }
        public string Role { get; set; } = "host";
        public ushort ProtocolMajor { get; set; } = WireProtocolVersion.CurrentMajor;
        public ushort ProtocolMinor { get; set; } = WireProtocolVersion.CurrentMinor;
        public List<string> RequiredCapabilities { get; } = [];
    }

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            return options.Mode switch
            {
                "serve" => await RunServerAsync(options, cancellation.Token).ConfigureAwait(false),
                "connect" => await RunClientAsync(options, cancellation.Token).ConfigureAwait(false),
                _ => throw new UsageException("The first argument must be either 'serve' or 'connect'.")
            };
        }
        catch (UsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitUsage;
        }
        catch (OperationCanceledException exception)
        {
            EmitDiagnostic("timeout", "protocol.operation_cancelled", exception.Message);
            return ExitTimeout;
        }
        catch (ProtocolException exception)
        {
            EmitDiagnostic("pipe", exception.Code, exception.Message);
            return ExitPipe;
        }
        catch (IOException exception)
        {
            EmitDiagnostic("pipe", "protocol.pipe_io_failed", exception.Message);
            return ExitPipe;
        }
        catch (Exception exception)
        {
            EmitDiagnostic("protocol", "protocol.unhandled_exception", exception.Message);
            return ExitProtocol;
        }
    }

    private static async Task<int> RunServerAsync(Options options, CancellationToken cancellationToken)
    {
        await using var server = new NamedPipeControlServer(options.Pipe);
        await using var connection = await server.AcceptAsync(cancellationToken).ConfigureAwait(false);
        var requestPayload = await connection.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (requestPayload is null)
        {
            EmitDiagnostic("pipe", "protocol.pipe_closed", "The client disconnected before sending a hello envelope.");
            return ExitPipe;
        }

        var decoded = ControlCodec.Decode(requestPayload);
        if (!decoded.IsSuccess)
        {
            EmitDiagnostic("protocol", decoded.Error!.Code, decoded.Error.Message);
            return ExitProtocol;
        }

        var response = HandshakeNegotiator.Negotiate(decoded.Value!, BuildPolicy());
        await connection.WriteAsync(ControlCodec.Encode(response), cancellationToken).ConfigureAwait(false);
        EmitSummary(response);
        return response.Type == "welcome" ? ExitSuccess : ExitProtocol;
    }

    private static async Task<int> RunClientAsync(Options options, CancellationToken cancellationToken)
    {
        await using var connection = await NamedPipeControlClient.ConnectAsync(options.Pipe, cancellationToken).ConfigureAwait(false);
        var hello = BuildHello(options);
        await connection.WriteAsync(ControlCodec.Encode(hello), cancellationToken).ConfigureAwait(false);
        var responsePayload = await connection.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (responsePayload is null)
        {
            EmitDiagnostic("pipe", "protocol.pipe_closed", "The server closed the connection before responding.");
            return ExitPipe;
        }

        var decoded = ControlCodec.Decode(responsePayload);
        if (!decoded.IsSuccess)
        {
            EmitDiagnostic("protocol", decoded.Error!.Code, decoded.Error.Message);
            return ExitProtocol;
        }

        var response = decoded.Value ?? throw new InvalidOperationException("The decoded response was unexpectedly null.");
        EmitSummary(response);
        return response.Type == "welcome" ? ExitSuccess : ExitProtocol;
    }

    private static ControlEnvelope BuildHello(Options options) =>
        new(
            new WireProtocolVersion(options.ProtocolMajor, options.ProtocolMinor),
            "hello",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Hello
            {
                Implementation = DotNetIdentity(),
                RequiredCapabilities = options.RequiredCapabilities,
                Role = ParseRole(options.Role),
                SupportedCapabilities = [ "control.handshake.v1", "story.run" ]
            });

    private static HandshakePolicy BuildPolicy() =>
        new()
        {
            LocalImplementation = DotNetIdentity(),
            LocalVersion = new WireProtocolVersion(WireProtocolVersion.CurrentMajor, WireProtocolVersion.CurrentMinor),
            SupportedCapabilities = [ "control.handshake.v1", "story.run" ],
            NextGuid = Guid.NewGuid,
            UtcNow = () => DateTimeOffset.UtcNow
        };

    private static ImplementationIdentity DotNetIdentity() =>
        new()
        {
            Binding = "ImGui.NET 1.91.6.1",
            DearImGuiIdentity = "ImGui.NET/ImGui.NET@8e26803be78b344fd68834817905405b3cdffb94",
            DearImGuiVersion = "1.91.6",
            Language = "csharp",
            Name = "DearStory.ProtocolProbe.DotNet",
            Toolchain = ".NET 10.0",
            Version = "0.1.0"
        };

    private static PeerRole ParseRole(string role) =>
        role switch
        {
            "runner" => PeerRole.Runner,
            "catalog" => PeerRole.Catalog,
            "host" => PeerRole.Host,
            _ => throw new UsageException("Role must be one of: runner, catalog, host.")
        };

    private static void EmitSummary(ControlEnvelope envelope)
    {
        if (envelope.Payload is Welcome welcome)
        {
            Console.WriteLine($"WELCOME protocol={envelope.Protocol.Major}.{envelope.Protocol.Minor} accepted={string.Join(',', welcome.AcceptedCapabilities)}");
            return;
        }

        if (envelope.Payload is Reject reject)
        {
            Console.WriteLine($"REJECT code={reject.Error.Code} recovery={reject.Error.Recovery}");
        }
    }

    private static void EmitDiagnostic(string category, string code, string message)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            category,
            code,
            message
        }));
    }

    private static Options ParseArguments(string[] args)
    {
        if (args.Length < 1)
        {
            throw new UsageException("Usage: DearStory.ProtocolProbe.DotNet <serve|connect> --pipe <name> [options]");
        }

        var options = new Options { Mode = args[0], Pipe = string.Empty };
        for (var index = 1; index < args.Length; index++)
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
                    options.Pipe = RequireValue("--pipe");
                    break;
                case "--role":
                    options.Role = RequireValue("--role");
                    break;
                case "--require":
                    options.RequiredCapabilities.Add(RequireValue("--require"));
                    break;
                case "--protocol-major":
                    options.ProtocolMajor = ushort.Parse(RequireValue("--protocol-major"), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--protocol-minor":
                    options.ProtocolMinor = ushort.Parse(RequireValue("--protocol-minor"), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--once":
                    break;
                default:
                    throw new UsageException($"Unknown argument: {argument}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.Pipe))
        {
            throw new UsageException("The --pipe argument is required.");
        }

        return options;
    }
}
