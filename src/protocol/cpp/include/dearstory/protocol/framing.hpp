#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include <dearstory/protocol/control_envelope.hpp>

namespace dearstory::protocol {

inline constexpr std::uint32_t max_control_frame_bytes = 1'048'576;

/// Represents the result of incrementally decoding one or more control frames.
class frame_push_result final {
public:
    explicit frame_push_result(std::vector<std::string> frames);
    explicit frame_push_result(protocol_error error);

    [[nodiscard]] bool has_value() const noexcept;
    [[nodiscard]] std::vector<std::string> const& value() const;
    [[nodiscard]] protocol_error const& error() const;

private:
    std::variant<std::vector<std::string>, protocol_error> storage_;
};

[[nodiscard]] std::vector<std::byte> frame(std::string_view payload);

/// Incrementally decodes length-prefixed UTF-8 control frames.
class frame_decoder final {
public:
    [[nodiscard]] frame_push_result push(std::span<std::byte const> bytes);

private:
    [[nodiscard]] static protocol_error make_invalid_envelope_error(std::string message);
    [[nodiscard]] static protocol_error make_frame_too_large_error(std::uint32_t declared_size);
    [[nodiscard]] static bool is_valid_utf8(std::span<std::byte const> bytes);

    std::array<std::byte, 4> prefix_buffer_{};
    std::size_t prefix_bytes_read_{ 0 };
    std::optional<std::uint32_t> expected_payload_size_{};
    std::vector<std::byte> payload_buffer_{};
    std::optional<protocol_error> terminal_error_{};
};

} // namespace dearstory::protocol
