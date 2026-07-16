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

/// Defines the maximum accepted control-frame payload size in bytes.
inline constexpr std::uint32_t max_control_frame_bytes = 1'048'576;

/// Represents the result of incrementally decoding one or more control frames.
class frame_push_result final {
public:
    /// Initializes a successful frame decoding result.
    /// \param frames The decoded UTF-8 JSON payloads extracted from the pushed bytes.
    explicit frame_push_result(std::vector<std::string> frames);
    /// Initializes a failed frame decoding result.
    /// \param error The framing or UTF-8 validation error.
    explicit frame_push_result(protocol_error error);

    /// Gets a value that indicates whether frame decoding succeeded.
    /// \returns \c true if decoded frames are available; otherwise, \c false.
    [[nodiscard]] bool has_value() const noexcept;
    /// Gets the decoded UTF-8 JSON payloads.
    /// \returns The decoded control-frame payload collection.
    [[nodiscard]] std::vector<std::string> const& value() const;
    /// Gets the framing failure payload.
    /// \returns The protocol error produced while decoding the frame stream.
    [[nodiscard]] protocol_error const& error() const;

private:
    std::variant<std::vector<std::string>, protocol_error> storage_;
};

/// Encodes one UTF-8 JSON payload into a length-prefixed DearStory control frame.
/// \param payload The UTF-8 JSON payload to frame.
/// \returns The length-prefixed frame bytes ready for transport.
[[nodiscard]] std::vector<std::byte> frame(std::string_view payload);

/// Incrementally decodes length-prefixed UTF-8 control frames.
class frame_decoder final {
public:
    /// Pushes one contiguous chunk of bytes into the incremental decoder.
    /// \param bytes The next byte span received from the transport.
    /// \returns The decoded frames when available, or the terminal protocol error.
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
