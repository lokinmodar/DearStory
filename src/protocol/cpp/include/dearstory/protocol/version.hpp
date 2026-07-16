#pragma once

#include <cstdint>
#include <optional>

namespace dearstory::protocol {

/// Identifies a DearStory wire-protocol version.
struct version final {
    std::uint16_t major{};
    std::uint16_t minor{};

    /// Returns true when both peers use the same protocol major.
    [[nodiscard]] constexpr bool is_major_compatible(version other) const noexcept
    {
        return major == other.major;
    }

    /// Chooses the shared major and the lower supported minor.
    [[nodiscard]] constexpr std::optional<version> negotiate(version other) const noexcept
    {
        if (!is_major_compatible(other)) {
            return std::nullopt;
        }

        return version{major, minor < other.minor ? minor : other.minor};
    }

    friend constexpr bool operator==(version, version) noexcept = default;
};

inline constexpr std::uint16_t current_major = 1;
inline constexpr std::uint16_t current_minor = 0;

} // namespace dearstory::protocol
