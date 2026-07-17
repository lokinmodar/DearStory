#pragma once

#include <optional>
#include <string>
#include <string_view>

namespace dearstory::core {

/// Represents a canonical, language-neutral DearStory story identifier.
class story_id final {
public:
    /// Parses and canonicalizes a raw story identifier.
    /// \param raw The raw story identifier text.
    /// \returns A canonical story identifier when parsing succeeds; otherwise, \c std::nullopt.
    [[nodiscard]] static std::optional<story_id> parse(std::string_view raw);

    /// Gets the canonical story identifier text.
    /// \returns The canonical identifier text.
    [[nodiscard]] std::string const& value() const noexcept;

    /// Compares two canonical story identifiers for equality.
    friend bool operator==(story_id const&, story_id const&) noexcept = default;
    /// Compares a canonical story identifier with raw text.
    friend bool operator==(story_id const& identifier, std::string_view raw) noexcept;
    /// Compares raw text with a canonical story identifier.
    friend bool operator==(std::string_view raw, story_id const& identifier) noexcept;

private:
    explicit story_id(std::string canonical_value);

    std::string value_;
};

} // namespace dearstory::core
