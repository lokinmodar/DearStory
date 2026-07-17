#pragma once

#include <dearstory/core/deterministic_clock.hpp>
#include <dearstory/core/deterministic_random.hpp>
#include <dearstory/core/story_id.hpp>

#include <chrono>
#include <cstdint>
#include <nlohmann/json.hpp>
#include <optional>
#include <string>

namespace dearstory::core {

/// Represents one active DearStory story session.
class story_session final {
public:
    /// Opens a new story session with deterministic services.
    /// \param session_id The session identifier text.
    /// \param story The canonical story identifier.
    /// \param initial_arguments The initial serializable argument snapshot.
    /// \param seed The deterministic seed value.
    /// \param start_time_utc The deterministic UTC start time.
    /// \returns A new active story session.
    [[nodiscard]] static story_session open(
        std::string session_id,
        story_id story,
        nlohmann::json initial_arguments,
        std::uint64_t seed,
        std::chrono::sys_time<std::chrono::milliseconds> start_time_utc);

    /// Gets the session identifier.
    /// \returns The session identifier text.
    [[nodiscard]] std::string const& session_id() const noexcept;

    /// Gets the canonical story identifier.
    /// \returns The canonical story identifier.
    [[nodiscard]] story_id const& story() const noexcept;

    /// Gets the active serializable arguments.
    /// \returns The active serializable arguments.
    [[nodiscard]] nlohmann::json& arguments() noexcept;

    /// Gets the active serializable arguments.
    /// \returns The active serializable arguments.
    [[nodiscard]] nlohmann::json const& arguments() const noexcept;

    /// Gets the deterministic clock for the session.
    /// \returns The deterministic clock.
    [[nodiscard]] deterministic_clock& clock() noexcept;

    /// Gets the deterministic clock for the session.
    /// \returns The deterministic clock.
    [[nodiscard]] deterministic_clock const& clock() const noexcept;

    /// Gets the deterministic random source for the session.
    /// \returns The deterministic random source.
    [[nodiscard]] deterministic_random& random() noexcept;

    /// Gets the deterministic random source for the session.
    /// \returns The deterministic random source.
    [[nodiscard]] deterministic_random const& random() const noexcept;

    /// Gets a value that indicates whether the session is closed.
    /// \returns \c true if the session is closed; otherwise, \c false.
    [[nodiscard]] bool is_closed() const noexcept;

    /// Gets the optional close reason.
    /// \returns The optional close reason text.
    [[nodiscard]] std::optional<std::string> const& close_reason() const noexcept;

    /// Resets the session to requested default arguments and deterministic services.
    /// \param default_arguments The default serializable argument snapshot.
    /// \param seed The deterministic seed value.
    /// \param start_time_utc The deterministic UTC start time.
    void reset(
        nlohmann::json const& default_arguments,
        std::uint64_t seed,
        std::chrono::sys_time<std::chrono::milliseconds> start_time_utc);

    /// Closes the session with an optional reason.
    /// \param reason The optional close reason.
    void close(std::optional<std::string> reason = std::nullopt);

private:
    story_session(
        std::string session_id,
        story_id story,
        nlohmann::json arguments,
        std::uint64_t seed,
        std::chrono::sys_time<std::chrono::milliseconds> start_time_utc);

    std::string session_id_;
    story_id story_;
    nlohmann::json arguments_;
    deterministic_clock clock_;
    deterministic_random random_;
    bool is_closed_{};
    std::optional<std::string> close_reason_{};
};

} // namespace dearstory::core
