#include <catch2/catch_test_macros.hpp>
#include <dearstory/sdk/argument_descriptor.hpp>

#include <string>

namespace
{
    struct primary_button_args final {
        std::string label{ "Save" };
    };
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

TEST_CASE("sdk_argument_descriptor preserves schema and default metadata")
{
    const auto metadata = dearstory::sdk::describe_arguments<primary_button_args>();

    REQUIRE(metadata.descriptors().size() == 1U);
    REQUIRE(metadata.default_arguments().at("label") == "Save");
    REQUIRE(metadata.descriptors().front().name == "label");
    REQUIRE(metadata.descriptors().front().default_value == "Save");
    REQUIRE(metadata.schema().document().at("properties").contains("label"));
}
