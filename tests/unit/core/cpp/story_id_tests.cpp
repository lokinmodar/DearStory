#include <catch2/catch_test_macros.hpp>
#include <dearstory/core/story_id.hpp>

TEST_CASE("story_id canonicalizes case and slash separators")
{
    REQUIRE(dearstory::core::story_id::parse("Buttons\\Primary").value() == "buttons/primary");
}

TEST_CASE("story_id trims surrounding whitespace")
{
    REQUIRE(dearstory::core::story_id::parse(" buttons/Primary ").value() == "buttons/primary");
}
