#pragma once

#include <dearstory/protocol/generated/messages.hpp>

#include <nlohmann/json.hpp>

#include <optional>
#include <string_view>
#include <vector>

namespace dearstory::core {

/// Represents one parsed DearStory argument schema document.
class argument_schema final {
public:
    /// Parses one DearStory argument schema JSON document.
    /// \param json The JSON document that uses the DearStory argument schema subset.
    /// \returns A parsed argument schema when parsing succeeds; otherwise, \c std::nullopt.
    [[nodiscard]] static std::optional<argument_schema> parse(std::string_view json);

    /// Parses one protocol-generated story argument schema document.
    /// \param schema The protocol-generated story argument schema.
    /// \returns A parsed argument schema when parsing succeeds; otherwise, \c std::nullopt.
    [[nodiscard]] static std::optional<argument_schema> from_protocol(protocol::generated::story_argument_schema const& schema);

    /// Gets the schema document.
    /// \returns The schema document.
    [[nodiscard]] nlohmann::json const& document() const noexcept;

private:
    explicit argument_schema(
        nlohmann::json document,
        std::vector<protocol::generated::field_diagnostic> schema_diagnostics);

    friend struct patch_result;
    friend patch_result apply_patch(argument_schema const&, nlohmann::json const&, nlohmann::json const&);

    nlohmann::json document_;
    std::vector<protocol::generated::field_diagnostic> schema_diagnostics_;
};

} // namespace dearstory::core
