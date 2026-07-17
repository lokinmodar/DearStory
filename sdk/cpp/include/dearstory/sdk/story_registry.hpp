#pragma once

#include <dearstory/sdk/story_registration.hpp>

#include <vector>

namespace dearstory::sdk {

/// Stores one host-local collection of story registrations.
class story_registry final {
public:
    /// Adds one story registration to the registry.
    /// \param registration The story registration to add.
    /// \throws std::invalid_argument A story with the same canonical ID is already registered.
    void add(story_registration registration);

    /// Gets the registered story descriptors sorted by canonical story ID.
    /// \returns The registered story descriptors sorted by canonical story ID.
    [[nodiscard]] std::vector<core::story_descriptor> descriptors() const;

    /// Gets the registered story metadata and callbacks.
    /// \returns The registered story metadata and callbacks.
    [[nodiscard]] std::vector<story_registration> const& registrations() const noexcept;

private:
    std::vector<story_registration> registrations_{};
};

} // namespace dearstory::sdk
