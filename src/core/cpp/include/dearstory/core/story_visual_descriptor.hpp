#pragma once

namespace dearstory::core {

/// Describes one story's visual-capture policy.
struct story_visual_descriptor final {
    /// Stores whether the story supports capture.
    bool supports_capture{ true };
    /// Stores whether the story is part of the canonical visual corpus.
    bool include_in_canonical_corpus{ false };

    /// Compares two visual descriptors for equality.
    friend bool operator==(story_visual_descriptor const&, story_visual_descriptor const&) = default;
};

} // namespace dearstory::core
