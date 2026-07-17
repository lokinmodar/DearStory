#pragma once

#include <dearstory/core/story_descriptor.hpp>

#include <map>
#include <string>
#include <string_view>
#include <vector>

namespace dearstory::core {

/// Represents one catalog merge diagnostic.
struct catalog_diagnostic final {
    /// Stores the stable diagnostic code.
    std::string code;
    /// Stores the human-readable diagnostic message.
    std::string message;
    /// Stores the host identifier that attempted the conflicting publication.
    std::string host_id;
    /// Stores the canonical story identifier associated with the diagnostic.
    story_id story;

    /// Compares two catalog diagnostics for equality.
    friend bool operator==(catalog_diagnostic const&, catalog_diagnostic const&) = default;
};

/// Represents the result of merging one host's publication into the story catalog.
struct catalog_merge_result final {
    /// Stores a value that indicates whether the merge completed without diagnostics.
    bool succeeded{};
    /// Stores the merged catalog stories sorted by canonical ID.
    std::vector<story_descriptor> stories{};
    /// Stores the diagnostics produced during the merge.
    std::vector<catalog_diagnostic> diagnostics{};

    /// Compares two catalog merge results for equality.
    friend bool operator==(catalog_merge_result const&, catalog_merge_result const&) = default;
};

/// Maintains the merged DearStory story catalog across hosts.
class story_catalog final {
public:
    /// Merges one host's published stories into the catalog.
    /// \param host_id The publishing host identifier.
    /// \param stories The stories published by the host.
    /// \returns A merge result describing the updated catalog state.
    [[nodiscard]] catalog_merge_result merge(std::string_view host_id, std::vector<story_descriptor> stories);

private:
    [[nodiscard]] std::vector<story_descriptor> merged_stories() const;

    std::map<std::string, story_descriptor, std::less<>> stories_{};
    std::map<std::string, std::string, std::less<>> story_hosts_{};
};

} // namespace dearstory::core
