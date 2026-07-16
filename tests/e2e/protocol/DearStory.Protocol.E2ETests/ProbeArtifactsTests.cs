using System.IO;
using Xunit;

namespace DearStory.Protocol.E2ETests;

public sealed class ProbeArtifactsTests
{
    [Fact]
    public void ResolveConfiguration_UsesBaseDirectoryWhenEnvironmentVariableIsMissing()
    {
        var previous = Environment.GetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION");
        try
        {
            Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", null);

            var configuration = ProbeArtifacts.ResolveConfiguration(@"C:\repo\tests\e2e\bin\Release\net10.0");

            Assert.Equal("Release", configuration);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", previous);
        }
    }

    [Fact]
    public void ResolveNativeProbe_UsesRequestedReleaseConfiguration()
    {
        var previous = Environment.GetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION");
        try
        {
            Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", "Release");
            var path = ProbeArtifacts.ResolveNativeProbe();

            Assert.Contains(Path.Combine("artifacts", "bin", "native", "Release"), path, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", previous);
        }
    }

    [Fact]
    public void ResolveManagedProbe_UsesRequestedReleaseConfiguration()
    {
        var previous = Environment.GetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION");
        try
        {
            Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", "Release");
            var path = ProbeArtifacts.ResolveManagedProbe();

            Assert.Contains(
                Path.Combine("tools", "DearStory.ProtocolProbe.DotNet", "bin", "Release", "net10.0"),
                path,
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEARSTORY_TEST_CONFIGURATION", previous);
        }
    }
}
