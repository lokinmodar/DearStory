#pragma once

#include <cstdint>
#include <optional>

namespace dearstory::protocol {

/// Identifies a DearStory wire-protocol version.
struct version final {
    /// Stores the breaking protocol generation.
    std::uint16_t major{};
    /// Stores the additive protocol generation.
    std::uint16_t minor{};

    /// Returns true when both peers use the same protocol major.
    /// \param other The version exposed by the remote peer.
    /// \returns \c true if the two versions share the same major; otherwise, \c false.
    [[nodiscard]] constexpr bool is_major_compatible(version other) const noexcept
    {
        return major == other.major;
    }

    /// Chooses the shared major and the lower supported minor.
    /// \param other The version exposed by the remote peer.
    /// \returns The negotiated protocol version when the majors match; otherwise, \c std::nullopt.
    [[nodiscard]] constexpr std::optional<version> negotiate(version other) const noexcept
    {
        if (!is_major_compatible(other)) {
            return std::nullopt;
        }

        return version{major, minor < other.minor ? minor : other.minor};
    }

    /// Compares two protocol versions for value equality.
    friend constexpr bool operator==(version, version) noexcept = default;
};

/// Identifies the first supported DearStory control-protocol major version.
inline constexpr std::uint16_t current_major = 1;
/// Identifies the first supported DearStory control-protocol minor version.
inline constexpr std::uint16_t current_minor = 0;

} // namespace dearstory::protocol
