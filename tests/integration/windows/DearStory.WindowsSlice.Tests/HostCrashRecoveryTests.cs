using Xunit;

namespace DearStory.WindowsSlice.Tests;

public sealed class HostCrashRecoveryTests
{
    [Fact]
    public async Task Dev_loop_reports_fault_when_the_restart_budget_is_exhausted()
    {
        await using var harness = await WindowsSliceHarness.StartAsync();

        await harness.SimulateHostCrashAsync("cpp-host");

        var fault = await harness.WaitForHostFaultAsync("cpp-host");

        Assert.Equal("cpp-host", fault.HostId);
        Assert.Equal(3, fault.RestartAttempts);
        Assert.Contains(fault.Diagnostics, diagnostic => diagnostic.Code == "runner.host.faulted");
    }
}
