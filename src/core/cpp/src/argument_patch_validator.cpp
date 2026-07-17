#include <dearstory/core/argument_patch_validator.hpp>

#include <algorithm>
#include <cmath>
#include <set>
#include <stdexcept>
#include <string>

namespace dearstory::core {
namespace
{
    [[nodiscard]] protocol::generated::field_diagnostic make_diagnostic(
        std::string code,
        std::string field,
        std::string message,
        std::optional<std::string> recovery = std::nullopt)
    {
        return protocol::generated::field_diagnostic{
            .code = std::move(code),
            .field = std::move(field),
            .message = std::move(message),
            .recovery = std::move(recovery),
        };
    }

    [[nodiscard]] bool is_supported_type(std::string const& value)
    {
        static const std::set<std::string, std::less<>> supported_types{
            "object",
            "boolean",
            "integer",
            "number",
            "string",
            "array",
        };

        return supported_types.contains(value);
    }

    [[nodiscard]] bool is_supported_keyword(std::string const& value)
    {
        static const std::set<std::string, std::less<>> supported_keywords{
            "type",
            "properties",
            "required",
            "enum",
            "minimum",
            "maximum",
            "minLength",
            "maxLength",
            "items",
            "default",
            "x-dearstory-control",
            "x-dearstory-order",
            "x-dearstory-category",
            "x-dearstory-visible",
        };

        return supported_keywords.contains(value);
    }

    void analyze_schema_node(
        nlohmann::json const& node,
        std::string const& path,
        std::vector<protocol::generated::field_diagnostic>& diagnostics)
    {
        if (!node.is_object())
        {
            throw std::invalid_argument("The DearStory argument schema node must be a JSON object.");
        }

        for (auto const& [keyword, value] : node.items())
        {
            auto const keyword_path = path + "." + keyword;
            if (!is_supported_keyword(keyword))
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.unsupported_keyword",
                        keyword_path,
                        "The schema keyword '" + keyword + "' is not supported by the DearStory argument subset.",
                        "Remove the unsupported keyword or replace it with a supported DearStory subset keyword."));
                continue;
            }

            if (keyword == "type")
            {
                if (!value.is_string() || !is_supported_type(value.get_ref<nlohmann::json::string_t const&>()))
                {
                    throw std::invalid_argument("The DearStory argument schema type must be one of the supported subset types.");
                }

                continue;
            }

            if (keyword == "properties")
            {
                if (!value.is_object())
                {
                    throw std::invalid_argument("The DearStory schema 'properties' keyword must be a JSON object.");
                }

                for (auto const& [property_name, property_schema] : value.items())
                {
                    analyze_schema_node(property_schema, keyword_path + "." + property_name, diagnostics);
                }

                continue;
            }

            if (keyword == "required" || keyword == "enum")
            {
                if (!value.is_array())
                {
                    throw std::invalid_argument("The DearStory schema array keyword must be a JSON array.");
                }

                if (keyword == "required")
                {
                    for (auto const& item : value)
                    {
                        if (!item.is_string())
                        {
                            throw std::invalid_argument("The DearStory schema 'required' keyword must contain only strings.");
                        }
                    }
                }

                continue;
            }

            if (keyword == "minimum" || keyword == "maximum")
            {
                if (!value.is_number())
                {
                    throw std::invalid_argument("The DearStory schema numeric keyword must be a JSON number.");
                }

                continue;
            }

            if (keyword == "minLength" || keyword == "maxLength" || keyword == "x-dearstory-order")
            {
                if (!value.is_number())
                {
                    throw std::invalid_argument("The DearStory schema integer keyword must be a JSON integer.");
                }

                auto const numeric_value = value.get<double>();
                if (std::floor(numeric_value) != numeric_value)
                {
                    throw std::invalid_argument("The DearStory schema integer keyword must be a JSON integer.");
                }

                continue;
            }

            if (keyword == "items")
            {
                analyze_schema_node(value, keyword_path, diagnostics);
                continue;
            }

            if (keyword == "x-dearstory-control" || keyword == "x-dearstory-category")
            {
                if (!value.is_string())
                {
                    throw std::invalid_argument("The DearStory schema annotation must be a string.");
                }

                continue;
            }

            if (keyword == "x-dearstory-visible")
            {
                if (!value.is_boolean())
                {
                    throw std::invalid_argument("The DearStory schema 'x-dearstory-visible' annotation must be a boolean.");
                }
            }
        }
    }

    [[nodiscard]] bool matches_type(std::string const& declared_type, nlohmann::json const& value)
    {
        if (declared_type == "object")
        {
            return value.is_object();
        }

        if (declared_type == "array")
        {
            return value.is_array();
        }

        if (declared_type == "string")
        {
            return value.is_string();
        }

        if (declared_type == "boolean")
        {
            return value.is_boolean();
        }

        if (declared_type == "number")
        {
            return value.is_number();
        }

        if (declared_type == "integer")
        {
            if (!value.is_number())
            {
                return false;
            }

            auto const numeric_value = value.get<double>();
            return std::floor(numeric_value) == numeric_value;
        }

        return false;
    }

    [[nodiscard]] std::string append_path(std::string const& base_path, std::string const& property_name)
    {
        return base_path + "." + property_name;
    }

    [[nodiscard]] nlohmann::json apply_merge_patch_impl(
        nlohmann::json const& current,
        nlohmann::json const& patch)
    {
        if (!patch.is_object())
        {
            return patch;
        }

        auto result = current.is_object() ? current : nlohmann::json::object();
        for (auto const& [property_name, property_value] : patch.items())
        {
            if (property_value.is_null())
            {
                result[property_name] = nullptr;
                continue;
            }

            auto const existing_value = result.contains(property_name)
                ? result.at(property_name)
                : nlohmann::json{};
            result[property_name] = apply_merge_patch_impl(existing_value, property_value);
        }

        return result;
    }

    void validate_node(
        nlohmann::json const& schema_node,
        nlohmann::json const& value,
        std::string const& field_path,
        std::vector<protocol::generated::field_diagnostic>& diagnostics)
    {
        auto const& schema_object = schema_node;

        if (schema_object.contains("type"))
        {
            auto const& declared_type = schema_object.at("type").get_ref<nlohmann::json::string_t const&>();
            if (!matches_type(declared_type, value))
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.type",
                        field_path,
                        "The value at '" + field_path + "' does not match the required '" + declared_type + "' type.",
                        "Provide a JSON value whose type is '" + declared_type + "'."));
                return;
            }
        }

        if (schema_object.contains("enum"))
        {
            auto const& enum_values = schema_object.at("enum");
            auto const matches = std::any_of(
                enum_values.begin(),
                enum_values.end(),
                [&value](nlohmann::json const& allowed) { return allowed == value; });
            if (!matches)
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.enum",
                        field_path,
                        "The value at '" + field_path + "' must match one of the declared enumeration values.",
                        "Choose one of the values declared by the schema enumeration."));
                return;
            }
        }

        if (schema_object.contains("minimum") && value.is_number())
        {
            auto const minimum = schema_object.at("minimum").get<double>();
            auto const numeric_value = value.get<double>();
            if (numeric_value < minimum)
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.minimum",
                        field_path,
                        "The value at '" + field_path + "' must be greater than or equal to " + std::to_string(minimum) + ".",
                        "Raise the numeric value to the declared minimum or above."));
            }
        }

        if (schema_object.contains("maximum") && value.is_number())
        {
            auto const maximum = schema_object.at("maximum").get<double>();
            auto const numeric_value = value.get<double>();
            if (numeric_value > maximum)
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.maximum",
                        field_path,
                        "The value at '" + field_path + "' must be less than or equal to " + std::to_string(maximum) + ".",
                        "Lower the numeric value to the declared maximum or below."));
            }
        }

        if (schema_object.contains("minLength") && value.is_string())
        {
            auto const minimum_length = schema_object.at("minLength").get<std::size_t>();
            auto const length = value.get_ref<nlohmann::json::string_t const&>().size();
            if (length < minimum_length)
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.min_length",
                        field_path,
                        "The value at '" + field_path + "' must have at least " + std::to_string(minimum_length) + " characters.",
                        "Provide a longer string value that satisfies the minimum length."));
            }
        }

        if (schema_object.contains("maxLength") && value.is_string())
        {
            auto const maximum_length = schema_object.at("maxLength").get<std::size_t>();
            auto const length = value.get_ref<nlohmann::json::string_t const&>().size();
            if (length > maximum_length)
            {
                diagnostics.push_back(
                    make_diagnostic(
                        "args.max_length",
                        field_path,
                        "The value at '" + field_path + "' must have at most " + std::to_string(maximum_length) + " characters.",
                        "Shorten the string value to satisfy the maximum length."));
            }
        }

        if (value.is_object())
        {
            auto const& object_value = value;

            if (schema_object.contains("required"))
            {
                for (auto const& required_property : schema_object.at("required"))
                {
                    auto const& property_name = required_property.get_ref<nlohmann::json::string_t const&>();
                    if (!object_value.contains(property_name))
                    {
                        diagnostics.push_back(
                            make_diagnostic(
                                "args.required",
                                append_path(field_path, property_name),
                                "The required property '" + property_name + "' is missing at '" + field_path + "'.",
                                "Add the '" + property_name + "' property to the argument payload."));
                    }
                }
            }

            if (schema_object.contains("properties"))
            {
                for (auto const& [property_name, property_schema] : schema_object.at("properties").items())
                {
                    if (object_value.contains(property_name))
                    {
                        validate_node(property_schema, object_value.at(property_name), append_path(field_path, property_name), diagnostics);
                    }
                }
            }
        }

        if (value.is_array() && schema_object.contains("items"))
        {
            auto index = std::size_t{ 0 };
            for (auto const& item_value : value)
            {
                validate_node(schema_object.at("items"), item_value, field_path + "[" + std::to_string(index) + "]", diagnostics);
                ++index;
            }
        }
    }
} // namespace

argument_schema::argument_schema(
    nlohmann::json document,
    std::vector<protocol::generated::field_diagnostic> schema_diagnostics)
    : document_(std::move(document))
    , schema_diagnostics_(std::move(schema_diagnostics))
{
}

std::optional<argument_schema> argument_schema::parse(std::string_view json)
{
    try
    {
        auto document = nlohmann::json::parse(json);
        std::vector<protocol::generated::field_diagnostic> diagnostics;
        analyze_schema_node(document, "$", diagnostics);
        return argument_schema(std::move(document), std::move(diagnostics));
    }
    catch (nlohmann::json::exception const&)
    {
        return std::nullopt;
    }
    catch (std::invalid_argument const&)
    {
        return std::nullopt;
    }
}

std::optional<argument_schema> argument_schema::from_protocol(protocol::generated::story_argument_schema const& schema)
{
    try
    {
        auto document = schema.schema;
        std::vector<protocol::generated::field_diagnostic> diagnostics;
        analyze_schema_node(document, "$", diagnostics);
        return argument_schema(std::move(document), std::move(diagnostics));
    }
    catch (std::invalid_argument const&)
    {
        return std::nullopt;
    }
}

nlohmann::json const& argument_schema::document() const noexcept
{
    return document_;
}

patch_result apply_patch(
    argument_schema const& schema,
    nlohmann::json const& current_arguments,
    nlohmann::json const& patch_document)
{
    if (!schema.schema_diagnostics_.empty())
    {
        return patch_result{
            .accepted = false,
            .updated_arguments = current_arguments,
            .diagnostics = schema.schema_diagnostics_,
        };
    }

    auto const candidate = apply_merge_patch_impl(current_arguments, patch_document);
    std::vector<protocol::generated::field_diagnostic> diagnostics;
    validate_node(schema.document_, candidate, "$", diagnostics);

    return patch_result{
        .accepted = diagnostics.empty(),
        .updated_arguments = diagnostics.empty() ? candidate : current_arguments,
        .diagnostics = std::move(diagnostics),
    };
}

} // namespace dearstory::core
