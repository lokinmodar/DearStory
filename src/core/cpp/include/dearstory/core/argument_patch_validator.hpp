#pragma once

#include <dearstory/core/argument_schema.hpp>

#include <nlohmann/json.hpp>

namespace dearstory::core {

/// Represents the result of validating and applying one DearStory argument patch.
struct patch_result final {
    /// Stores a value that indicates whether the patch was accepted.
    bool accepted{};
    /// Stores the updated argument snapshot.
    nlohmann::json updated_arguments{};
    /// Stores the diagnostics produced while applying the patch.
    std::vector<protocol::generated::field_diagnostic> diagnostics{};

    /// Compares two patch results for equality.
    friend bool operator==(patch_result const&, patch_result const&) = default;
};

/// Applies one patch document to the current arguments and validates the resulting payload.
/// \param schema The parsed DearStory argument schema.
/// \param current_arguments The current argument snapshot.
/// \param patch_document The requested patch document.
/// \returns A patch result describing acceptance, the updated arguments, and any diagnostics.
[[nodiscard]] patch_result apply_patch(
    argument_schema const& schema,
    nlohmann::json const& current_arguments,
    nlohmann::json const& patch_document);

} // namespace dearstory::core
