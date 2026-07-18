#include <imgui.h>
#include <nlohmann/json.hpp>

#include <dearstory/core/argument_schema.hpp>
#include <dearstory/sdk/story_registry.hpp>

#include <string>

namespace dearstory::examples::windows_slice
{
    struct buttons_primary_args final {
        std::string label{ "Save" };
    };

    namespace
    {
        [[nodiscard]] sdk::argument_metadata build_buttons_primary_arguments()
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
                        .description = "Caption shown on the primary button.",
                    },
                });
        }

        void buttons_primary(sdk::story_context& context)
        {
            std::string label = "Save";
            auto const& arguments = context.args();
            if (auto const iterator = arguments.find("label");
                iterator != arguments.end() && iterator->is_string())
            {
                label = iterator->get<std::string>();
            }

            (void)ImGui::Button(label.c_str());
        }
    } // namespace

    void register_windows_slice_cpp_stories(sdk::story_registry& registry)
    {
        registry.add(sdk::story_registration::create(
            "Buttons/Primary",
            buttons_primary,
            build_buttons_primary_arguments(),
            sdk::visual_story_options{ .include_in_canonical_corpus = true }));
    }
}
