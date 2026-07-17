#pragma once

#include <dearstory/core/action_event.hpp>
#include <dearstory/core/interaction_target.hpp>
#include <dearstory/core/log_event.hpp>
#include <dearstory/core/story_session.hpp>

#include <vector>

namespace dearstory::sdk {

/// Exposes the DearStory core session state and emitted artifacts to one story callback.
class story_context final {
public:
    /// Initializes a new instance of the story context wrapper.
    /// \param session The active story session.
    explicit story_context(core::story_session& session) noexcept
        : session_(session)
    {
    }

    /// Gets the active story session.
    /// \returns The active story session.
    [[nodiscard]] core::story_session& session() noexcept { return session_; }

    /// Gets the active story session.
    /// \returns The active story session.
    [[nodiscard]] core::story_session const& session() const noexcept { return session_; }

    /// Gets the active serializable arguments.
    /// \returns The active serializable arguments.
    [[nodiscard]] nlohmann::json& args() noexcept { return session_.arguments(); }

    /// Gets the active serializable arguments.
    /// \returns The active serializable arguments.
    [[nodiscard]] nlohmann::json const& args() const noexcept { return session_.arguments(); }

    /// Gets the collected action events for the current callback execution.
    /// \returns The collected action events.
    [[nodiscard]] std::vector<core::action_event>& actions() noexcept { return actions_; }

    /// Gets the collected action events for the current callback execution.
    /// \returns The collected action events.
    [[nodiscard]] std::vector<core::action_event> const& actions() const noexcept { return actions_; }

    /// Gets the collected log events for the current callback execution.
    /// \returns The collected log events.
    [[nodiscard]] std::vector<core::log_event>& logs() noexcept { return logs_; }

    /// Gets the collected log events for the current callback execution.
    /// \returns The collected log events.
    [[nodiscard]] std::vector<core::log_event> const& logs() const noexcept { return logs_; }

    /// Gets the collected interaction targets for the current callback execution.
    /// \returns The collected interaction targets.
    [[nodiscard]] std::vector<core::interaction_target>& targets() noexcept { return targets_; }

    /// Gets the collected interaction targets for the current callback execution.
    /// \returns The collected interaction targets.
    [[nodiscard]] std::vector<core::interaction_target> const& targets() const noexcept { return targets_; }

    /// Gets the deterministic clock for the current session.
    /// \returns The deterministic clock.
    [[nodiscard]] core::deterministic_clock& clock() noexcept { return session_.clock(); }

    /// Gets the deterministic clock for the current session.
    /// \returns The deterministic clock.
    [[nodiscard]] core::deterministic_clock const& clock() const noexcept { return session_.clock(); }

    /// Gets the deterministic random source for the current session.
    /// \returns The deterministic random source.
    [[nodiscard]] core::deterministic_random& random() noexcept { return session_.random(); }

    /// Gets the deterministic random source for the current session.
    /// \returns The deterministic random source.
    [[nodiscard]] core::deterministic_random const& random() const noexcept { return session_.random(); }

private:
    core::story_session& session_;
    std::vector<core::action_event> actions_{};
    std::vector<core::log_event> logs_{};
    std::vector<core::interaction_target> targets_{};
};

} // namespace dearstory::sdk
