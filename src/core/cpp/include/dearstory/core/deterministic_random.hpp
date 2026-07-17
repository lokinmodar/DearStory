#pragma once

#include <cstdint>

namespace dearstory::core {

/// Provides a deterministic pseudo-random sequence for one story session.
class deterministic_random final {
public:
    /// Initializes a new instance of the deterministic random source.
    /// \param seed The initial seed value.
    explicit deterministic_random(std::uint64_t seed) noexcept;

    /// Resets the pseudo-random sequence to a requested seed.
    /// \param seed The seed value.
    void reset(std::uint64_t seed) noexcept;

    /// Gets the next deterministic unsigned 32-bit value.
    /// \returns The next unsigned 32-bit pseudo-random value.
    [[nodiscard]] std::uint32_t next_uint32() noexcept;

private:
    std::uint64_t state_{};
};

inline deterministic_random::deterministic_random(std::uint64_t seed) noexcept
{
    reset(seed);
}

inline void deterministic_random::reset(std::uint64_t seed) noexcept
{
    state_ = seed;
    if (state_ == 0U)
    {
        state_ = 0x9E3779B97F4A7C15ULL;
    }
}

inline std::uint32_t deterministic_random::next_uint32() noexcept
{
    state_ ^= state_ >> 12;
    state_ ^= state_ << 25;
    state_ ^= state_ >> 27;
    auto const value = state_ * 2685821657736338717ULL;
    return static_cast<std::uint32_t>(value >> 32);
}

} // namespace dearstory::core
