using DearStory.Runner.Configuration;
using Xunit;

namespace DearStory.Runner.Tests;

public sealed class WorkspaceConfigurationLoaderTests
{
    [Fact]
    public void LoadFromText_finds_dearstory_toml_and_binds_hosts()
    {
        var config = WorkspaceConfigurationLoader.LoadFromText(
            """
            [workspace]
            name = "windows-slice"

            [[hosts]]
            id = "cpp-host"
            builder = "cmake"

            [[hosts]]
            id = "dotnet-host"
            builder = "dotnet"
            """);

        Assert.Equal("windows-slice", config.Workspace.Name);
        Assert.Collection(
            config.Hosts,
            host => Assert.Equal("cpp-host", host.Id),
            host => Assert.Equal("dotnet-host", host.Id));
    }
}
