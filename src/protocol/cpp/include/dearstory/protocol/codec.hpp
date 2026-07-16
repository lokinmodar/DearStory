#pragma once

#include <string>
#include <string_view>
#include <variant>

#include <dearstory/protocol/control_envelope.hpp>

namespace dearstory::protocol {

/// Represents the result of decoding a control envelope.
class decode_result final {
public:
    /// Initializes a successful decode result.
    /// \param value The decoded control envelope.
    explicit decode_result(control_envelope value);
    /// Initializes a failed decode result.
    /// \param error The protocol validation error produced while decoding.
    explicit decode_result(protocol_error error);

    /// Gets a value that indicates whether decoding succeeded.
    /// \returns \c true if a decoded envelope is available; otherwise, \c false.
    [[nodiscard]] bool has_value() const noexcept;
    /// Gets the decoded envelope.
    /// \returns The successfully decoded envelope.
    [[nodiscard]] control_envelope const& value() const;
    /// Gets the decoded envelope through dereference syntax.
    /// \returns The successfully decoded envelope.
    [[nodiscard]] control_envelope const& operator*() const;
    /// Gets the decoded envelope through pointer-like syntax.
    /// \returns A pointer to the successfully decoded envelope.
    [[nodiscard]] control_envelope const* operator->() const;
    /// Gets the decode failure payload.
    /// \returns The protocol error produced while decoding.
    [[nodiscard]] protocol_error const& error() const;

private:
    std::variant<control_envelope, protocol_error> storage_;
};

/// Decodes one UTF-8 JSON control envelope payload.
/// \param json The UTF-8 JSON payload to decode.
/// \returns A successful result containing the decoded envelope, or a failure containing the protocol error.
[[nodiscard]] decode_result decode(std::string_view json);
/// Encodes one control envelope into canonical UTF-8 JSON.
/// \param envelope The envelope to encode.
/// \returns The encoded UTF-8 JSON payload.
[[nodiscard]] std::string encode(control_envelope const& envelope);

} // namespace dearstory::protocol
