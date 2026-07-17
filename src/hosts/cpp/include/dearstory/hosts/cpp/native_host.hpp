#pragma once

#include <string>

namespace dearstory::hosts::cpp {

/// Describes the startup inputs required by the native DearStory host.
struct native_host_options final {
    /// Stores the named-pipe endpoint published by the runner-side harness.
    std::string pipe_name{};
    /// Stores the stable host identifier echoed into story-index payloads.
    std::string host_id{};
};

/// Runs the Windows-first native DearStory host baseline for official C++ stories.
class native_host final {
public:
    /// Initializes a new instance of the native_host class.
    /// \param options The startup inputs required to connect and identify the host.
    explicit native_host(native_host_options options);

    /// Connects to the control pipe, publishes the story index, and serves one native session loop.
    /// \returns Zero when execution completes successfully; otherwise, a non-zero process exit code.
    [[nodiscard]] int run();

private:
    native_host_options options_;
};

} // namespace dearstory::hosts::cpp
