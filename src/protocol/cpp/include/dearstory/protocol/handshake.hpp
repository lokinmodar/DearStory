#pragma once

#include <functional>
#include <string>
#include <vector>

#include <dearstory/protocol/control_envelope.hpp>

namespace dearstory::protocol {

/// Describes the local peer data required to negotiate one hello envelope.
struct handshake_policy final {
    /// Declares the local protocol version supported by this peer.
    version local_version{ current_major, current_minor };

    /// Declares the local implementation identity echoed into diagnostics.
    generated::implementation_identity local_implementation{};

    /// Declares the capabilities supported by the local peer.
    std::vector<std::string> supported_capabilities{};

    /// Produces lowercase RFC 4122 UUID strings for response identifiers.
    std::function<std::string()> create_uuid{};

    /// Produces RFC 3339 UTC timestamps with millisecond precision.
    std::function<std::string()> create_timestamp{};
};

/// Negotiates a hello control envelope into a welcome or reject envelope.
/// \param hello_envelope The initiating hello envelope sent by the remote peer.
/// \param policy The local negotiation policy applied to the hello message.
/// \returns A welcome envelope when negotiation succeeds; otherwise, a reject envelope.
[[nodiscard]] control_envelope negotiate(control_envelope const& hello_envelope, handshake_policy const& policy);

} // namespace dearstory::protocol
