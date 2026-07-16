#pragma once

#include <string>
#include <string_view>
#include <variant>

#include <dearstory/protocol/control_envelope.hpp>

namespace dearstory::protocol {

/// Represents the result of decoding a control envelope.
class decode_result final {
public:
    explicit decode_result(control_envelope value);
    explicit decode_result(protocol_error error);

    [[nodiscard]] bool has_value() const noexcept;
    [[nodiscard]] control_envelope const& value() const;
    [[nodiscard]] control_envelope const& operator*() const;
    [[nodiscard]] control_envelope const* operator->() const;
    [[nodiscard]] protocol_error const& error() const;

private:
    std::variant<control_envelope, protocol_error> storage_;
};

[[nodiscard]] decode_result decode(std::string_view json);
[[nodiscard]] std::string encode(control_envelope const& envelope);

} // namespace dearstory::protocol
