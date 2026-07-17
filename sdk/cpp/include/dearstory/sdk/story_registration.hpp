#pragma once

#include <dearstory/sdk/argument_descriptor.hpp>
#include <dearstory/sdk/story_context.hpp>

#include <dearstory/core/story_descriptor.hpp>

#include <string_view>

namespace dearstory::sdk {

/// Stores one registered DearStory C++ story callback and its metadata.
class story_registration final {
public:
    /// Represents one DearStory C++ story callback.
    using story_callback = void (*)(story_context&);

    /// Creates one story registration from raw ID text, callback, and argument metadata.
    /// \param raw_id The raw story identifier text.
    /// \param render The story callback function.
    /// \param arguments The compile-time supplied argument metadata.
    /// \returns A canonical story registration.
    [[nodiscard]] static story_registration create(
        std::string_view raw_id,
        story_callback render,
        argument_metadata arguments);

    /// Gets the canonical story descriptor.
    /// \returns The canonical story descriptor.
    [[nodiscard]] core::story_descriptor const& descriptor() const noexcept;

    /// Gets the compile-time supplied argument metadata.
    /// \returns The compile-time supplied argument metadata.
    [[nodiscard]] argument_metadata const& arguments() const noexcept;

    /// Gets the story callback function.
    /// \returns The story callback function.
    [[nodiscard]] story_callback callback() const noexcept;

private:
    story_registration(
        core::story_descriptor descriptor,
        argument_metadata arguments,
        story_callback render) noexcept;

    core::story_descriptor descriptor_;
    argument_metadata arguments_;
    story_callback render_{};
};

} // namespace dearstory::sdk
