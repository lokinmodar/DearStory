namespace DearStory.Runner;

/// <summary>Defines the stable process exit codes for the DearStory Windows runner.</summary>
public enum RunnerExitCode
{
    /// <summary>Indicates that the command completed successfully.</summary>
    Success = 0,

    /// <summary>Indicates that workspace discovery, command parsing, or configuration binding failed.</summary>
    ConfigurationFailure = 10,

    /// <summary>Indicates that a build step failed.</summary>
    BuildFailure = 20,

    /// <summary>Indicates that control-channel or transport protocol setup failed.</summary>
    ProtocolFailure = 30,

    /// <summary>Indicates that one or more host processes failed to start or exhausted restart attempts.</summary>
    HostLaunchFailure = 40
}
