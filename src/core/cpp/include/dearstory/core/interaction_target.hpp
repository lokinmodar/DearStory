#pragma once

#include <nlohmann/json.hpp>

#include <optional>
#include <string>

namespace dearstory::core {

/// Represents optional semantic metadata for one interaction target.
struct interaction_target_semantic_metadata final {
    /// Stores the optional semantic role.
    std::optional<std::string> role{};
    /// Stores the optional accessible name.
    std::optional<std::string> accessible_name{};
    /// Stores the optional semantic description.
    std::optional<std::string> description{};

    /// Compares two semantic metadata values for equality.
    friend bool operator==(interaction_target_semantic_metadata const&, interaction_target_semantic_metadata const&) = default;
};

/// Represents one named interaction target reported by a story.
struct interaction_target final {
    /// Stores the stable target identifier.
    std::string id;
    /// Stores the optional serialized bounds payload.
    std::optional<nlohmann::json> bounds{};
    /// Stores the optional semantic metadata.
    std::optional<interaction_target_semantic_metadata> semantic{};

    /// Compares two interaction targets for equality.
    friend bool operator==(interaction_target const&, interaction_target const&) = default;
};

} // namespace dearstory::core
