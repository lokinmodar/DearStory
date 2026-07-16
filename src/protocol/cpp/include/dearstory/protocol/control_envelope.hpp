#pragma once

#include <optional>
#include <string>
#include <variant>

#include <dearstory/protocol/generated/messages.hpp>
#include <dearstory/protocol/version.hpp>

namespace dearstory::protocol {

/// Aliases the generated protocol error payload used across the public API.
using protocol_error = generated::protocol_error;
/// Aliases the supported control payload union used by the DearStory envelope surface.
using control_payload = std::variant<generated::hello, generated::welcome, generated::reject>;

/// Represents a decoded DearStory control envelope.
struct control_envelope final {
    /// Identifies the envelope protocol version.
    version protocol{};

    /// Stores the control message type.
    std::string type{};

    /// Stores the unique message identifier.
    std::string message_id{};

    /// Stores the optional correlation identifier.
    std::optional<std::string> correlation_id{};

    /// Stores the optional session identifier.
    std::optional<std::string> session_id{};

    /// Stores the RFC 3339 UTC timestamp.
    std::string timestamp{};

    /// Stores the typed payload selected by \ref type.
    control_payload payload{};

    /// Compares two control envelopes for value equality.
    friend bool operator==(control_envelope const&, control_envelope const&) = default;
};

} // namespace dearstory::protocol
