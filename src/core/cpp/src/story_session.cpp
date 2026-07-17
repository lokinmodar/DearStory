#include <dearstory/core/story_session.hpp>

#include <utility>

namespace dearstory::core {

story_session::story_session(
    std::string session_id,
    story_id story,
    nlohmann::json arguments,
    std::uint64_t seed,
    std::chrono::sys_time<std::chrono::milliseconds> start_time_utc)
    : session_id_(std::move(session_id))
    , story_(std::move(story))
    , arguments_(std::move(arguments))
    , clock_(start_time_utc)
    , random_(seed)
{
}

story_session story_session::open(
    std::string session_id,
    story_id story,
    nlohmann::json initial_arguments,
    std::uint64_t seed,
    std::chrono::sys_time<std::chrono::milliseconds> start_time_utc)
{
    return story_session(
        std::move(session_id),
        std::move(story),
        std::move(initial_arguments),
        seed,
        start_time_utc);
}

std::string const& story_session::session_id() const noexcept
{
    return session_id_;
}

story_id const& story_session::story() const noexcept
{
    return story_;
}

nlohmann::json& story_session::arguments() noexcept
{
    return arguments_;
}

nlohmann::json const& story_session::arguments() const noexcept
{
    return arguments_;
}

deterministic_clock& story_session::clock() noexcept
{
    return clock_;
}

deterministic_clock const& story_session::clock() const noexcept
{
    return clock_;
}

deterministic_random& story_session::random() noexcept
{
    return random_;
}

deterministic_random const& story_session::random() const noexcept
{
    return random_;
}

bool story_session::is_closed() const noexcept
{
    return is_closed_;
}

std::optional<std::string> const& story_session::close_reason() const noexcept
{
    return close_reason_;
}

void story_session::reset(
    nlohmann::json const& default_arguments,
    std::uint64_t seed,
    std::chrono::sys_time<std::chrono::milliseconds> start_time_utc)
{
    arguments_ = default_arguments;
    clock_.reset(start_time_utc);
    random_.reset(seed);
    is_closed_ = false;
    close_reason_.reset();
}

void story_session::close(std::optional<std::string> reason)
{
    is_closed_ = true;
    close_reason_ = std::move(reason);
}

} // namespace dearstory::core
