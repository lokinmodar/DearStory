#include <dearstory/protocol/codec.hpp>

#include <algorithm>
#include <array>
#include <regex>
#include <stdexcept>

#include <nlohmann/json.hpp>

namespace dearstory::protocol {
namespace
{
    protocol_error make_protocol_error(std::string code, std::string message, std::string recovery)
    {
        return generated::protocol_error{
            .code = std::move(code),
            .message = std::move(message),
            .recovery = std::move(recovery)
        };
    }

    protocol_error make_invalid_envelope_error(std::string message)
    {
        return make_protocol_error(
            "protocol.invalid_envelope",
            std::move(message),
            "Resend a valid DearStory control envelope.");
    }

    bool is_uuid(std::string const& value)
    {
        static const std::regex pattern(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            std::regex::ECMAScript);
        return std::regex_match(value, pattern);
    }

    bool is_timestamp(std::string const& value)
    {
        static const std::regex pattern(
            "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z$",
            std::regex::ECMAScript);
        return std::regex_match(value, pattern);
    }

    std::string require_string(nlohmann::json const& object, char const* name)
    {
        auto const iterator = object.find(name);
        if (iterator == object.end() || !iterator->is_string())
        {
            throw std::runtime_error(std::string("Missing or invalid string field '") + name + "'.");
        }

        return iterator->get<std::string>();
    }

    std::optional<std::string> optional_string(nlohmann::json const& object, char const* name)
    {
        auto const iterator = object.find(name);
        if (iterator == object.end())
        {
            return std::nullopt;
        }

        if (!iterator->is_string())
        {
            throw std::runtime_error(std::string("Invalid string field '") + name + "'.");
        }

        return iterator->get<std::string>();
    }

    std::vector<std::string> require_string_array(nlohmann::json const& object, char const* name)
    {
        auto const iterator = object.find(name);
        if (iterator == object.end() || !iterator->is_array())
        {
            throw std::runtime_error(std::string("Missing or invalid string array field '") + name + "'.");
        }

        std::vector<std::string> values;
        values.reserve(iterator->size());
        for (auto const& element : *iterator)
        {
            if (!element.is_string())
            {
                throw std::runtime_error(std::string("Field '") + name + "' contains a non-string entry.");
            }

            values.push_back(element.get<std::string>());
        }

        return values;
    }

    generated::peer_role parse_peer_role(std::string const& role)
    {
        if (role == "runner")
        {
            return generated::peer_role::runner;
        }

        if (role == "catalog")
        {
            return generated::peer_role::catalog;
        }

        if (role == "host")
        {
            return generated::peer_role::host;
        }

        throw std::runtime_error("The 'role' field is invalid.");
    }

    std::string serialize_peer_role(generated::peer_role role)
    {
        switch (role)
        {
        case generated::peer_role::runner:
            return "runner";
        case generated::peer_role::catalog:
            return "catalog";
        case generated::peer_role::host:
            return "host";
        }

        throw std::runtime_error("The peer role is invalid.");
    }

    generated::protocol_version parse_generated_version(nlohmann::json const& object)
    {
        auto const major = object.find("major");
        auto const minor = object.find("minor");
        if (major == object.end() || minor == object.end() || !major->is_number_unsigned() || !minor->is_number_unsigned())
        {
            throw std::runtime_error("The protocol version payload is invalid.");
        }

        if (object.size() != 2)
        {
            throw std::runtime_error("The protocol version payload contains unsupported fields.");
        }

        return generated::protocol_version{
            .major = major->get<std::uint16_t>(),
            .minor = minor->get<std::uint16_t>()
        };
    }

    version parse_envelope_version(nlohmann::json const& object)
    {
        auto const payload_version = parse_generated_version(object);
        if (payload_version.major != current_major)
        {
            throw std::runtime_error("The envelope protocol major is unsupported.");
        }

        return version{ payload_version.major, payload_version.minor };
    }

    generated::implementation_identity parse_implementation_identity(nlohmann::json const& object)
    {
        return generated::implementation_identity{
            .binding = optional_string(object, "binding"),
            .dearImGuiIdentity = optional_string(object, "dearImGuiIdentity"),
            .dearImGuiVersion = optional_string(object, "dearImGuiVersion"),
            .language = require_string(object, "language"),
            .name = require_string(object, "name"),
            .toolchain = require_string(object, "toolchain"),
            .version = require_string(object, "version")
        };
    }

    generated::protocol_error parse_protocol_error(nlohmann::json const& object)
    {
        generated::protocol_error error{
            .code = require_string(object, "code"),
            .message = require_string(object, "message"),
            .recovery = require_string(object, "recovery")
        };

        if (auto const details = object.find("details"); details != object.end())
        {
            if (!details->is_object())
            {
                throw std::runtime_error("The 'details' field must be an object.");
            }

            error.details = *details;
        }

        return error;
    }

    generated::hello parse_hello(nlohmann::json const& object)
    {
        auto const implementation = object.find("implementation");
        if (implementation == object.end() || !implementation->is_object())
        {
            throw std::runtime_error("The 'implementation' field is invalid.");
        }

        return generated::hello{
            .implementation = parse_implementation_identity(*implementation),
            .requiredCapabilities = require_string_array(object, "requiredCapabilities"),
            .role = parse_peer_role(require_string(object, "role")),
            .supportedCapabilities = require_string_array(object, "supportedCapabilities")
        };
    }

    generated::welcome parse_welcome(nlohmann::json const& object)
    {
        auto const negotiated_version = object.find("negotiatedVersion");
        if (negotiated_version == object.end() || !negotiated_version->is_object())
        {
            throw std::runtime_error("The 'negotiatedVersion' field is invalid.");
        }

        auto const peer_id = require_string(object, "peerId");
        if (!is_uuid(peer_id))
        {
            throw std::runtime_error("The 'peerId' field is not a valid UUID.");
        }

        return generated::welcome{
            .acceptedCapabilities = require_string_array(object, "acceptedCapabilities"),
            .negotiatedVersion = parse_generated_version(*negotiated_version),
            .peerId = peer_id
        };
    }

    generated::reject parse_reject(nlohmann::json const& object)
    {
        auto const error = object.find("error");
        if (error == object.end() || !error->is_object())
        {
            throw std::runtime_error("The 'error' field is invalid.");
        }

        return generated::reject{
            .error = parse_protocol_error(*error)
        };
    }

    nlohmann::json to_json(generated::implementation_identity const& value)
    {
        nlohmann::json json{
            { "language", value.language },
            { "name", value.name },
            { "toolchain", value.toolchain },
            { "version", value.version }
        };

        if (value.binding.has_value())
        {
            json["binding"] = *value.binding;
        }

        if (value.dearImGuiIdentity.has_value())
        {
            json["dearImGuiIdentity"] = *value.dearImGuiIdentity;
        }

        if (value.dearImGuiVersion.has_value())
        {
            json["dearImGuiVersion"] = *value.dearImGuiVersion;
        }

        return json;
    }

    nlohmann::json to_json(generated::protocol_error const& value)
    {
        nlohmann::json json{
            { "code", value.code },
            { "message", value.message },
            { "recovery", value.recovery }
        };

        if (value.details.has_value())
        {
            json["details"] = *value.details;
        }

        return json;
    }

    nlohmann::json to_json(generated::protocol_version const& value)
    {
        return nlohmann::json{
            { "major", value.major },
            { "minor", value.minor }
        };
    }

    nlohmann::json to_json(generated::hello const& value)
    {
        return nlohmann::json{
            { "implementation", to_json(value.implementation) },
            { "requiredCapabilities", value.requiredCapabilities },
            { "role", serialize_peer_role(value.role) },
            { "supportedCapabilities", value.supportedCapabilities }
        };
    }

    nlohmann::json to_json(generated::welcome const& value)
    {
        return nlohmann::json{
            { "acceptedCapabilities", value.acceptedCapabilities },
            { "negotiatedVersion", to_json(value.negotiatedVersion) },
            { "peerId", value.peerId }
        };
    }

    nlohmann::json to_json(generated::reject const& value)
    {
        return nlohmann::json{
            { "error", to_json(value.error) }
        };
    }
} // namespace

decode_result::decode_result(control_envelope value)
    : storage_(std::move(value))
{
}

decode_result::decode_result(protocol_error error)
    : storage_(std::move(error))
{
}

bool decode_result::has_value() const noexcept
{
    return std::holds_alternative<control_envelope>(storage_);
}

control_envelope const& decode_result::value() const
{
    return std::get<control_envelope>(storage_);
}

control_envelope const& decode_result::operator*() const
{
    return value();
}

control_envelope const* decode_result::operator->() const
{
    return &value();
}

protocol_error const& decode_result::error() const
{
    return std::get<protocol_error>(storage_);
}

decode_result decode(std::string_view json)
{
    try
    {
        auto const document = nlohmann::json::parse(json);
        if (!document.is_object())
        {
            return decode_result(make_invalid_envelope_error("The control envelope must be a JSON object."));
        }

        auto const protocol_node = document.find("protocol");
        auto const payload_node = document.find("payload");
        if (protocol_node == document.end() || !protocol_node->is_object() || payload_node == document.end() || !payload_node->is_object())
        {
            return decode_result(make_invalid_envelope_error("The control envelope is missing a required object field."));
        }

        control_envelope envelope{
            .protocol = parse_envelope_version(*protocol_node),
            .type = require_string(document, "type"),
            .message_id = require_string(document, "messageId"),
            .correlation_id = optional_string(document, "correlationId"),
            .session_id = optional_string(document, "sessionId"),
            .timestamp = require_string(document, "timestamp")
        };

        if (!is_uuid(envelope.message_id))
        {
            return decode_result(make_invalid_envelope_error("The 'messageId' field must be a lowercase RFC 4122 UUID."));
        }

        if (envelope.correlation_id.has_value() && !is_uuid(*envelope.correlation_id))
        {
            return decode_result(make_invalid_envelope_error("The 'correlationId' field must be a lowercase RFC 4122 UUID."));
        }

        if (envelope.session_id.has_value() && !is_uuid(*envelope.session_id))
        {
            return decode_result(make_invalid_envelope_error("The 'sessionId' field must be a lowercase RFC 4122 UUID."));
        }

        if (!is_timestamp(envelope.timestamp))
        {
            return decode_result(make_invalid_envelope_error("The 'timestamp' field must be an RFC 3339 UTC timestamp with millisecond precision."));
        }

        if (envelope.type == "hello")
        {
            envelope.payload = parse_hello(*payload_node);
        }
        else if (envelope.type == "welcome")
        {
            envelope.payload = parse_welcome(*payload_node);
        }
        else if (envelope.type == "reject")
        {
            envelope.payload = parse_reject(*payload_node);
        }
        else
        {
            return decode_result(make_protocol_error(
                "protocol.unknown_message_type",
                "The control envelope type is unsupported.",
                "Use one of: hello, welcome, reject."));
        }

        return decode_result(std::move(envelope));
    }
    catch (std::exception const& exception)
    {
        return decode_result(make_invalid_envelope_error(exception.what()));
    }
}

std::string encode(control_envelope const& envelope)
{
    nlohmann::json payload;
    std::visit(
        [&payload](auto const& typed_payload)
        {
            payload = to_json(typed_payload);
        },
        envelope.payload);

    nlohmann::json json{
        { "protocol", { { "major", envelope.protocol.major }, { "minor", envelope.protocol.minor } } },
        { "type", envelope.type },
        { "messageId", envelope.message_id },
        { "timestamp", envelope.timestamp },
        { "payload", std::move(payload) }
    };

    if (envelope.correlation_id.has_value())
    {
        json["correlationId"] = *envelope.correlation_id;
    }

    if (envelope.session_id.has_value())
    {
        json["sessionId"] = *envelope.session_id;
    }

    return json.dump();
}

} // namespace dearstory::protocol
