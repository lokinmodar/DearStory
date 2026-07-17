using DearStory.Runner.Supervision;
using Xunit;

namespace DearStory.Runner.Tests;

public sealed class HostSupervisorTests
{
    [Fact]
    public async Task Restart_policy_stops_after_bounded_retries()
    {
        var supervisor = new HostSupervisor(maxRestartAttempts: 3);
        var launch = HostLaunchDescriptor.Failing("cpp-host");

        var result = await supervisor.RunUntilTerminalAsync(launch, CancellationToken.None);

        Assert.Equal(3, result.RestartAttempts);
        Assert.Equal(HostTerminalState.Faulted, result.State);
    }
}
