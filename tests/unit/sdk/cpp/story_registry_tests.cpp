#include <catch2/catch_test_macros.hpp>
#include <dearstory/sdk/macros.hpp>
#include <dearstory/sdk/story_registry.hpp>

#include <string>
#include <vector>

namespace
{
    struct primary_button_args final {
        std::string label{ "Save" };
    };

    void primary_button(dearstory::sdk::story_context&)
    {
    }
}

template <>
dearstory::sdk::argument_metadata dearstory::sdk::describe_arguments<primary_button_args>()
{
    auto schema = dearstory::core::argument_schema::parse(
        R"({
            "type":"object",
            "properties":{
                "label":{
                    "type":"string",
                    "default":"Save",
                    "x-dearstory-control":"text"
                }
            },
            "required":["label"]
        })");

    return dearstory::sdk::argument_metadata(
        std::move(schema).value(),
        nlohmann::json::parse(R"({"label":"Save"})"),
        {
            dearstory::sdk::argument_descriptor{
                .name = "label",
                .schema = nlohmann::json::parse(R"({"type":"string","default":"Save","x-dearstory-control":"text"})"),
                .default_value = "Save",
                .description = "Caption shown on the button.",
            },
        });
}

TEST_CASE("sdk_story_registry produces a canonical descriptor without wrapping ImGui")
{
    dearstory::sdk::story_registry registry;
    registry.add(DEARSTORY_STORY("Buttons/Primary", primary_button, primary_button_args));

    auto const descriptors = registry.descriptors();
    REQUIRE(descriptors.size() == 1U);
    REQUIRE(descriptors.front().id.value() == "buttons/primary");
    REQUIRE(descriptors.front().title == "Primary");
    REQUIRE(descriptors.front().hierarchy == std::vector<std::string>{ "Buttons" });

    auto const& registrations = registry.registrations();
    REQUIRE(registrations.size() == 1U);
    REQUIRE(registrations.front().arguments().default_arguments().at("label") == "Save");
}

TEST_CASE("sdk_story_registration keeps explicit canonical visual enrollment")
{
    auto registration = dearstory::sdk::story_registration::create(
        "buttons/primary",
        +[](dearstory::sdk::story_context&) {},
        dearstory::sdk::argument_metadata::empty(),
        dearstory::sdk::visual_story_options{ .include_in_canonical_corpus = true });

    REQUIRE(registration.descriptor().visual.supports_capture);
    REQUIRE(registration.descriptor().visual.include_in_canonical_corpus);
}
