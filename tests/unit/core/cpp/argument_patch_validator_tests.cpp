#include <catch2/catch_test_macros.hpp>
#include <dearstory/core/argument_patch_validator.hpp>
#include <dearstory/core/argument_schema.hpp>

#include <filesystem>
#include <fstream>
#include <sstream>
#include <string_view>

namespace
{
    nlohmann::json read_vector(std::string_view file_name)
    {
        const auto path = std::filesystem::path(DEARSTORY_REPOSITORY_ROOT) / "tests" / "contract" / "core" / "vectors" / file_name;
        std::ifstream stream(path, std::ios::binary);
        REQUIRE(stream.good());

        std::ostringstream buffer;
        buffer << stream.rdbuf();
        return nlohmann::json::parse(buffer.str());
    }
}

TEST_CASE("argument_patch_validator accepts the shared valid patch vector")
{
    const auto vector = read_vector("patch-valid.string.json");
    auto schema = dearstory::core::argument_schema::parse(vector.at("schema").dump()).value();

    const auto result = dearstory::core::apply_patch(
        schema,
        vector.at("currentArguments"),
        vector.at("patch"));

    REQUIRE(result.accepted);
    REQUIRE(result.diagnostics.empty());
    REQUIRE(result.updated_arguments == vector.at("expected").at("updatedArguments"));
}

TEST_CASE("argument_patch_validator rejects the shared invalid enum vector")
{
    const auto vector = read_vector("patch-invalid.enum.json");
    auto schema = dearstory::core::argument_schema::parse(vector.at("schema").dump()).value();

    const auto result = dearstory::core::apply_patch(
        schema,
        vector.at("currentArguments"),
        vector.at("patch"));

    REQUIRE_FALSE(result.accepted);
    REQUIRE(result.diagnostics.size() == 1U);
    REQUIRE(result.diagnostics.front().code == "args.enum");
    REQUIRE(result.updated_arguments == vector.at("expected").at("updatedArguments"));
}

TEST_CASE("argument_patch_validator rejects unsupported schema keywords")
{
    auto schema = dearstory::core::argument_schema::parse(R"({
        "type":"object",
        "properties":{"label":{"type":"string","pattern":"^[A-Z]+$"}}
    })").value();

    auto result = dearstory::core::apply_patch(
        schema,
        nlohmann::json::parse(R"({"label":"Save"})"),
        nlohmann::json::parse(R"({"label":"Discard"})"));

    REQUIRE_FALSE(result.accepted);
    REQUIRE(result.diagnostics.size() == 1U);
    REQUIRE(result.diagnostics.front().code == "args.unsupported_keyword");
    REQUIRE(result.updated_arguments == nlohmann::json::parse(R"({"label":"Save"})"));
}
