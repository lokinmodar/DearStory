#pragma once

#include <dearstory/core/argument_schema.hpp>

#include <nlohmann/json.hpp>

#include <optional>
#include <string>
#include <type_traits>
#include <vector>

namespace dearstory::sdk {

/// Describes one serializable story argument entry.
struct argument_descriptor final {
    /// Stores the stable argument name.
    std::string name;
    /// Stores the schema fragment for the argument.
    nlohmann::json schema;
    /// Stores the default serialized value for the argument.
    nlohmann::json default_value;
    /// Stores the optional human-readable description.
    std::optional<std::string> description{};

    /// Compares two argument descriptors for equality.
    friend bool operator==(argument_descriptor const&, argument_descriptor const&) = default;
};

/// Stores the schema and default metadata for one story argument type.
class argument_metadata final {
public:
    /// Initializes a new instance of the argument metadata bundle.
    /// \param schema The parsed DearStory argument schema.
    /// \param default_arguments The default serialized argument snapshot.
    /// \param descriptors The per-argument metadata entries.
    argument_metadata(
        core::argument_schema schema,
        nlohmann::json default_arguments,
        std::vector<argument_descriptor> descriptors);

    /// Creates an empty object-shaped argument metadata bundle.
    /// \returns An empty argument metadata bundle.
    [[nodiscard]] static argument_metadata empty();

    /// Gets the parsed DearStory argument schema.
    /// \returns The parsed DearStory argument schema.
    [[nodiscard]] core::argument_schema const& schema() const noexcept;

    /// Gets the default serialized argument snapshot.
    /// \returns The default serialized argument snapshot.
    [[nodiscard]] nlohmann::json const& default_arguments() const noexcept;

    /// Gets the per-argument metadata entries.
    /// \returns The per-argument metadata entries.
    [[nodiscard]] std::vector<argument_descriptor> const& descriptors() const noexcept;

    /// Compares two argument metadata bundles for equality.
    friend bool operator==(argument_metadata const&, argument_metadata const&) = default;

private:
    core::argument_schema schema_;
    nlohmann::json default_arguments_;
    std::vector<argument_descriptor> descriptors_;
};

inline argument_metadata::argument_metadata(
    core::argument_schema schema,
    nlohmann::json default_arguments,
    std::vector<argument_descriptor> descriptors)
    : schema_(std::move(schema))
    , default_arguments_(std::move(default_arguments))
    , descriptors_(std::move(descriptors))
{
}

inline argument_metadata argument_metadata::empty()
{
    auto schema = core::argument_schema::parse(R"({"type":"object","properties":{}})");
    return argument_metadata(std::move(schema).value(), nlohmann::json::object(), {});
}

inline core::argument_schema const& argument_metadata::schema() const noexcept
{
    return schema_;
}

inline nlohmann::json const& argument_metadata::default_arguments() const noexcept
{
    return default_arguments_;
}

inline std::vector<argument_descriptor> const& argument_metadata::descriptors() const noexcept
{
    return descriptors_;
}

template <typename>
inline constexpr bool always_false_v = false;

/// Describes one C++ argument type for DearStory story registration.
/// \tparam T The story argument type.
/// \returns A compile-time supplied metadata bundle for the argument type.
template <typename T>
[[nodiscard]] argument_metadata describe_arguments()
{
    static_assert(always_false_v<T>, "Provide a dearstory::sdk::describe_arguments<T>() specialization for this argument type.");
}

} // namespace dearstory::sdk
