#pragma once

#include <chrono>

namespace dearstory::core {

/// Provides an explicitly advanced deterministic clock for one story session.
class deterministic_clock final {
public:
    /// Initializes a new instance of the deterministic clock.
    /// \param initial_utc The initial UTC time value.
    explicit deterministic_clock(std::chrono::sys_time<std::chrono::milliseconds> initial_utc) noexcept;

    /// Gets the current deterministic UTC time.
    /// \returns The current deterministic UTC time.
    [[nodiscard]] std::chrono::sys_time<std::chrono::milliseconds> current_utc() const noexcept;

    /// Advances the clock by a requested duration.
    /// \tparam Rep The duration representation type.
    /// \tparam Period The duration period type.
    /// \param delta The duration to add to the current time.
    template <typename Rep, typename Period>
    void advance(std::chrono::duration<Rep, Period> delta) noexcept
    {
        current_utc_ += std::chrono::duration_cast<std::chrono::milliseconds>(delta);
    }

    /// Resets the clock to a requested UTC time.
    /// \param value The UTC time value.
    void reset(std::chrono::sys_time<std::chrono::milliseconds> value) noexcept;

private:
    std::chrono::sys_time<std::chrono::milliseconds> current_utc_;
};

inline deterministic_clock::deterministic_clock(std::chrono::sys_time<std::chrono::milliseconds> initial_utc) noexcept
    : current_utc_(initial_utc)
{
}

inline std::chrono::sys_time<std::chrono::milliseconds> deterministic_clock::current_utc() const noexcept
{
    return current_utc_;
}

inline void deterministic_clock::reset(std::chrono::sys_time<std::chrono::milliseconds> value) noexcept
{
    current_utc_ = value;
}

} // namespace dearstory::core
