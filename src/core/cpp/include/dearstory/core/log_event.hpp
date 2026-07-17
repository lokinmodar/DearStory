#pragma once

#include <chrono>
#include <nlohmann/json.hpp>
#include <optional>
#include <string>

namespace dearstory::core {

/// Represents one emitted story log event.
struct log_event final {
    /// Stores the log level.
    std::string level;
    /// Stores the log message text.
    std::string message;
    /// Stores the UTC time when the log was emitted.
    std::chrono::sys_time<std::chrono::milliseconds> emitted_at_utc{};
    /// Stores the optional structured log details.
    std::optional<nlohmann::json> details{};

    /// Compares two log events for equality.
    friend bool operator==(log_event const&, log_event const&) = default;
};

} // namespace dearstory::core
