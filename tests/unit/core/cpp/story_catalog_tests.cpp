#include <catch2/catch_test_macros.hpp>
#include <dearstory/core/story_catalog.hpp>

#include <vector>

TEST_CASE("story_catalog rejects duplicate ids from different hosts")
{
    dearstory::core::story_catalog catalog;
    auto const first = catalog.merge(
        "cpp-host",
        std::vector<dearstory::core::story_descriptor>{
            dearstory::core::story_descriptor::create("buttons/primary", "Buttons/Primary") });
    auto const second = catalog.merge(
        "dotnet-host",
        std::vector<dearstory::core::story_descriptor>{
            dearstory::core::story_descriptor::create("Buttons/Primary", "Buttons/Primary") });

    REQUIRE(first.succeeded);
    REQUIRE_FALSE(second.succeeded);
    REQUIRE(second.diagnostics.size() == 1U);
    REQUIRE(second.diagnostics.front().code == "story.duplicate_id");
}

TEST_CASE("story_catalog returns stories sorted by canonical id")
{
    dearstory::core::story_catalog catalog;

    auto const result = catalog.merge(
        "cpp-host",
        std::vector<dearstory::core::story_descriptor>{
            dearstory::core::story_descriptor::create("buttons/secondary", "Buttons/Secondary"),
            dearstory::core::story_descriptor::create("buttons/primary", "Buttons/Primary") });

    REQUIRE(result.succeeded);
    REQUIRE(result.stories.size() == 2U);
    REQUIRE(result.stories.at(0).id.value() == "buttons/primary");
    REQUIRE(result.stories.at(1).id.value() == "buttons/secondary");
}
