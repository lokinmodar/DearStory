#pragma once

#include <chrono>
#include <nlohmann/json.hpp>
#include <optional>
#include <string>

namespace dearstory::core {

/// Represents one emitted story action event.
struct action_event final {
    /// Stores the stable action name.
    std::string name;
    /// Stores the serializable action payload.
    nlohmann::json payload;
    /// Stores the UTC time when the action was emitted.
    std::chrono::sys_time<std::chrono::milliseconds> emitted_at_utc{};
    /// Stores the optional associated target identifier.
    std::optional<std::string> target_id{};

    /// Compares two action events for equality.
    friend bool operator==(action_event const&, action_event const&) = default;
};

} // namespace dearstory::core
