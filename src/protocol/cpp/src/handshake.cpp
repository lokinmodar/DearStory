#include <dearstory/protocol/handshake.hpp>

#include <algorithm>
#include <stdexcept>
#include <string_view>

namespace dearstory::protocol {
namespace
{
    [[nodiscard]] std::vector<std::string> sorted_unique(std::vector<std::string> values)
    {
        std::sort(values.begin(), values.end());
        values.erase(std::unique(values.begin(), values.end()), values.end());
        return values;
    }

    [[nodiscard]] bool has_duplicates(std::vector<std::string> const& values)
    {
        return sorted_unique(values).size() != values.size();
    }

    [[nodiscard]] std::string join_recovery(version local_version)
    {
        return "Retry with protocol " + std::to_string(local_version.major) + "." + std::to_string(local_version.minor) + ".";
    }

    [[nodiscard]] control_envelope make_reject(
        control_envelope const& hello_envelope,
        handshake_policy const& policy,
        std::string code,
        std::string message,
        std::string recovery)
    {
        if (!policy.create_uuid || !policy.create_timestamp)
        {
            throw std::invalid_argument("The handshake policy must provide UUID and timestamp factories.");
        }

        return control_envelope{
            .protocol = policy.local_version,
            .type = "reject",
            .message_id = policy.create_uuid(),
            .correlation_id = hello_envelope.message_id,
            .timestamp = policy.create_timestamp(),
            .payload = generated::reject{
                .error = generated::protocol_error{
                    .code = std::move(code),
                    .message = std::move(message),
                    .recovery = std::move(recovery)
                } }
        };
    }
} // namespace

control_envelope negotiate(control_envelope const& hello_envelope, handshake_policy const& policy)
{
    if (!policy.create_uuid || !policy.create_timestamp)
    {
        throw std::invalid_argument("The handshake policy must provide UUID and timestamp factories.");
    }

    if (hello_envelope.type != "hello" ||
        !std::holds_alternative<generated::hello>(hello_envelope.payload))
    {
        return make_reject(
            hello_envelope,
            policy,
            "protocol.invalid_envelope",
            "The handshake requires a hello control envelope.",
            "Resend a valid hello envelope.");
    }

    auto const* hello = std::get_if<generated::hello>(&hello_envelope.payload);
    if (hello == nullptr)
    {
        return make_reject(
            hello_envelope,
            policy,
            "protocol.invalid_envelope",
            "The handshake hello payload is missing.",
            "Resend a valid hello envelope.");
    }

    if (has_duplicates(hello->supportedCapabilities) ||
        has_duplicates(hello->requiredCapabilities))
    {
        return make_reject(
            hello_envelope,
            policy,
            "protocol.invalid_envelope",
            "The hello envelope contains duplicate capabilities.",
            "Resend the hello envelope with unique capability names.");
    }

    auto const negotiated_version = policy.local_version.negotiate(hello_envelope.protocol);
    if (!negotiated_version.has_value())
    {
        return make_reject(
            hello_envelope,
            policy,
            "protocol.major_mismatch",
            "The remote peer uses an unsupported protocol major.",
            join_recovery(policy.local_version));
    }

    auto const local_supported = sorted_unique(policy.supported_capabilities);
    for (auto const& required : hello->requiredCapabilities)
    {
        if (!std::binary_search(local_supported.begin(), local_supported.end(), required))
        {
            return make_reject(
                hello_envelope,
                policy,
                "protocol.required_capability_missing",
                "The remote peer requires an unsupported capability: " + required + ".",
                "Retry after enabling the required capability: " + required + ".");
        }
    }

    auto remote_supported = sorted_unique(hello->supportedCapabilities);
    std::vector<std::string> accepted_capabilities;
    std::set_intersection(
        local_supported.begin(),
        local_supported.end(),
        remote_supported.begin(),
        remote_supported.end(),
        std::back_inserter(accepted_capabilities));

    auto const message_id = policy.create_uuid();
    auto const session_id = policy.create_uuid();
    auto const peer_id = policy.create_uuid();

    return control_envelope{
        .protocol = *negotiated_version,
        .type = "welcome",
        .message_id = message_id,
        .correlation_id = hello_envelope.message_id,
        .session_id = session_id,
        .timestamp = policy.create_timestamp(),
        .payload = generated::welcome{
            .acceptedCapabilities = std::move(accepted_capabilities),
            .negotiatedVersion = generated::protocol_version{
                .major = negotiated_version->major,
                .minor = negotiated_version->minor },
            .peerId = peer_id } };
}

} // namespace dearstory::protocol
