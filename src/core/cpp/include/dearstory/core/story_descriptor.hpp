#pragma once

#include <dearstory/core/story_id.hpp>
#include <dearstory/core/story_visual_descriptor.hpp>

#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace dearstory::core {

/// Describes one published DearStory story.
struct story_descriptor final {
    /// Stores the canonical story identifier.
    story_id id;
    /// Stores the human-facing story title.
    std::string title;
    /// Stores the story hierarchy segments.
    std::vector<std::string> hierarchy{};
    /// Stores the story tag values.
    std::vector<std::string> tags{};
    /// Stores the optional story description.
    std::optional<std::string> description{};
    /// Stores the optional source-path hint for the story.
    std::optional<std::string> source_path{};
    /// Stores the declared capability values for the story.
    std::vector<std::string> capabilities{};
    /// Stores the visual regression metadata for the story.
    story_visual_descriptor visual{};

    /// Creates a minimal story descriptor from a raw ID and title.
    /// \param raw_id The raw story identifier text.
    /// \param story_title The display title.
    /// \returns A canonical story descriptor.
    /// \throws std::invalid_argument The story identifier is empty after canonicalization.
    [[nodiscard]] static story_descriptor create(std::string_view raw_id, std::string_view story_title);

    /// Compares two descriptors for equality.
    friend bool operator==(story_descriptor const&, story_descriptor const&) = default;
};

} // namespace dearstory::core
