#pragma once

#include <stop_token>
#include <string_view>

#include <dearstory/protocol/windows/named_pipe_server.hpp>

namespace dearstory::protocol::windows {

/// Opens client-side DearStory control connections over Windows named pipes.
class named_pipe_client final {
public:
    /// Connects to one named-pipe control server.
    [[nodiscard]] static pipe_connection connect(std::wstring_view pipe_name, std::stop_token stop_token);
};

} // namespace dearstory::protocol::windows
