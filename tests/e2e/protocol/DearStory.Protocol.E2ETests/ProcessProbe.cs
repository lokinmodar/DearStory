using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace DearStory.Protocol.E2ETests;

internal static class ProcessProbe
{
    internal static RunningProbe StartNative(params string[] arguments) =>
        RunningProbe.Start(ResolveNativeExecutable(), arguments);

    internal static RunningProbe StartManaged(params string[] arguments) =>
        RunningProbe.Start(ResolveManagedExecutable(), arguments);

    internal static Task<ProbeResult> RunNativeAsync(params string[] arguments) =>
        RunningProbe.RunOnceAsync(ResolveNativeExecutable(), arguments, TestContext.Current.CancellationToken);

    internal static Task<ProbeResult> RunManagedAsync(params string[] arguments) =>
        RunningProbe.RunOnceAsync(ResolveManagedExecutable(), arguments, TestContext.Current.CancellationToken);

    internal static async Task WaitForPipeAsync(string pipeName, CancellationToken cancellationToken)
    {
        var fullPipeName = pipeName.StartsWith(@"\\.\pipe\", StringComparison.Ordinal)
            ? pipeName
            : $@"\\.\pipe\{pipeName}";

        while (!cancellationToken.IsCancellationRequested)
        {
            if (WaitNamedPipe(fullPipeName, 50))
            {
                return;
            }

            var error = Marshal.GetLastWin32Error();
            if (error is not 0 and not 2 and not 121)
            {
                throw new InvalidOperationException($"WaitNamedPipe failed for '{fullPipeName}' with Win32 error {error}.");
            }

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ResolveNativeExecutable() =>
        Path.Combine(ResolveRepositoryRoot(), "artifacts", "bin", "native", "Debug", "dearstory-protocol-probe-cpp.exe");

    private static string ResolveManagedExecutable() =>
        Path.Combine(
            ResolveRepositoryRoot(),
            "tools",
            "DearStory.ProtocolProbe.DotNet",
            "bin",
            "Debug",
            "net10.0",
            "DearStory.ProtocolProbe.DotNet.exe");

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DearStory.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the DearStory repository root.");
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WaitNamedPipeW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WaitNamedPipe(string name, int timeout);
}

internal sealed class RunningProbe : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _executable;
    private readonly string[] _arguments;
    private readonly Task<string> _stdout;
    private readonly Task<string> _stderr;

    private RunningProbe(Process process, string executable, string[] arguments)
    {
        _process = process;
        _executable = executable;
        _arguments = arguments;
        _stdout = process.StandardOutput.ReadToEndAsync();
        _stderr = process.StandardError.ReadToEndAsync();
    }

    internal static RunningProbe Start(string executable, params string[] arguments)
    {
        var startInfo = CreateStartInfo(executable, arguments);
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start probe executable '{executable}'.");
        return new RunningProbe(process, executable, arguments);
    }

    internal static async Task<ProbeResult> RunOnceAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        await using var running = Start(executable, arguments);
        var exitCode = await running.WaitForExitAsync().ConfigureAwait(false);
        return new ProbeResult(
            exitCode,
            await running.StandardOutputAsync.ConfigureAwait(false),
            await running.StandardErrorAsync.ConfigureAwait(false));
    }

    internal Task<string> StandardOutputAsync => _stdout;

    internal Task<string> StandardErrorAsync => _stderr;

    internal async Task<int> WaitForExitAsync()
    {
        await _process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return _process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static ProcessStartInfo CreateStartInfo(string executable, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}

internal sealed record ProbeResult(int ExitCode, string StandardOutput, string StandardError);
