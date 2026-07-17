#include <catch2/catch_test_macros.hpp>
#include <dearstory/core/story_session.hpp>

#include <chrono>
#include <nlohmann/json.hpp>

TEST_CASE("story_session opens with the requested initial state")
{
    auto const session = dearstory::core::story_session::open(
        "11111111-1111-4111-8111-111111111111",
        dearstory::core::story_id::parse("buttons/primary").value(),
        nlohmann::json::parse(R"({"label":"Save"})"),
        42U,
        std::chrono::sys_days{ std::chrono::July / 16 / 2026 } + std::chrono::hours{ 12 });

    REQUIRE(session.story().value() == "buttons/primary");
    REQUIRE(session.arguments().at("label") == "Save");
    REQUIRE_FALSE(session.is_closed());
}

TEST_CASE("story_session reset restores default arguments and deterministic services")
{
    auto session = dearstory::core::story_session::open(
        "11111111-1111-4111-8111-111111111111",
        dearstory::core::story_id::parse("buttons/primary").value(),
        nlohmann::json::parse(R"({"label":"Discard"})"),
        7U,
        std::chrono::sys_days{ std::chrono::July / 16 / 2026 } + std::chrono::hours{ 12 } + std::chrono::minutes{ 10 });

    session.arguments()["label"] = "Discard";
    session.clock().advance(std::chrono::seconds{ 30 });
    static_cast<void>(session.random().next_uint32());

    session.reset(
        nlohmann::json::parse(R"({"label":"Save"})"),
        42U,
        std::chrono::sys_days{ std::chrono::July / 16 / 2026 } + std::chrono::hours{ 12 });

    dearstory::core::deterministic_random expected_random{ 42U };

    REQUIRE(session.arguments().at("label") == "Save");
    REQUIRE(session.clock().current_utc() == (std::chrono::sys_days{ std::chrono::July / 16 / 2026 } + std::chrono::hours{ 12 }));
    REQUIRE(session.random().next_uint32() == expected_random.next_uint32());
    REQUIRE_FALSE(session.is_closed());
}

TEST_CASE("story_session close records a close reason")
{
    auto session = dearstory::core::story_session::open(
        "11111111-1111-4111-8111-111111111111",
        dearstory::core::story_id::parse("buttons/primary").value(),
        nlohmann::json::parse(R"({"label":"Save"})"),
        1U,
        std::chrono::sys_days{ std::chrono::July / 16 / 2026 } + std::chrono::hours{ 12 });

    session.close("completed");

    REQUIRE(session.is_closed());
    REQUIRE(session.close_reason() == "completed");
}
