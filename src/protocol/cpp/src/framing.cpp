#include <dearstory/protocol/framing.hpp>

#include <bit>
#include <stdexcept>

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
} // namespace

frame_push_result::frame_push_result(std::vector<std::string> frames)
    : storage_(std::move(frames))
{
}

frame_push_result::frame_push_result(protocol_error error)
    : storage_(std::move(error))
{
}

bool frame_push_result::has_value() const noexcept
{
    return std::holds_alternative<std::vector<std::string>>(storage_);
}

std::vector<std::string> const& frame_push_result::value() const
{
    return std::get<std::vector<std::string>>(storage_);
}

protocol_error const& frame_push_result::error() const
{
    return std::get<protocol_error>(storage_);
}

std::vector<std::byte> frame(std::string_view payload)
{
    if (payload.size() > max_control_frame_bytes)
    {
        throw std::invalid_argument("The payload exceeds the DearStory control frame size limit.");
    }

    std::vector<std::byte> bytes(sizeof(std::uint32_t) + payload.size());
    auto const prefix = std::bit_cast<std::array<std::byte, sizeof(std::uint32_t)>>(static_cast<std::uint32_t>(payload.size()));
    std::copy(prefix.begin(), prefix.end(), bytes.begin());
    for (std::size_t index = 0; index < payload.size(); ++index)
    {
        bytes[sizeof(std::uint32_t) + index] = static_cast<std::byte>(payload[index]);
    }

    return bytes;
}

frame_push_result frame_decoder::push(std::span<std::byte const> bytes)
{
    if (terminal_error_.has_value())
    {
        return frame_push_result(*terminal_error_);
    }

    std::vector<std::string> frames;

    for (auto const byte : bytes)
    {
        if (!expected_payload_size_.has_value())
        {
            prefix_buffer_[prefix_bytes_read_++] = byte;
            if (prefix_bytes_read_ < prefix_buffer_.size())
            {
                continue;
            }

            auto const declared_size = std::bit_cast<std::uint32_t>(prefix_buffer_);
            prefix_bytes_read_ = 0;
            if (declared_size > max_control_frame_bytes)
            {
                terminal_error_ = make_frame_too_large_error(declared_size);
                return frame_push_result(*terminal_error_);
            }

            expected_payload_size_ = declared_size;
            if (declared_size == 0)
            {
                frames.emplace_back();
                expected_payload_size_.reset();
            }

            continue;
        }

        payload_buffer_.push_back(byte);
        if (payload_buffer_.size() < *expected_payload_size_)
        {
            continue;
        }

        if (!is_valid_utf8(payload_buffer_))
        {
            terminal_error_ = make_invalid_envelope_error("The frame payload is not valid UTF-8.");
            return frame_push_result(*terminal_error_);
        }

        std::string frame_text;
        frame_text.reserve(payload_buffer_.size());
        for (auto const payload_byte : payload_buffer_)
        {
            frame_text.push_back(static_cast<char>(payload_byte));
        }

        frames.push_back(std::move(frame_text));
        payload_buffer_.clear();
        expected_payload_size_.reset();
    }

    return frame_push_result(std::move(frames));
}

protocol_error frame_decoder::make_invalid_envelope_error(std::string message)
{
    return make_protocol_error(
        "protocol.invalid_envelope",
        std::move(message),
        "Discard the current decoder instance and resend a valid UTF-8 frame.");
}

protocol_error frame_decoder::make_frame_too_large_error(std::uint32_t declared_size)
{
    protocol_error error = make_protocol_error(
        "protocol.frame_too_large",
        "The declared control frame exceeds the 1 MiB limit.",
        "Reduce the control payload size and resend the frame.");
    error.details = nlohmann::json{ { "declaredSize", declared_size } };
    return error;
}

bool frame_decoder::is_valid_utf8(std::span<std::byte const> bytes)
{
    std::size_t index = 0;
    while (index < bytes.size())
    {
        auto const lead = std::to_integer<unsigned char>(bytes[index]);
        std::size_t continuation_count = 0;
        unsigned char minimum = 0;

        if ((lead & 0x80U) == 0U)
        {
            ++index;
            continue;
        }

        if ((lead & 0xE0U) == 0xC0U)
        {
            continuation_count = 1;
            minimum = 0x80U;
        }
        else if ((lead & 0xF0U) == 0xE0U)
        {
            continuation_count = 2;
            minimum = 0x800U >> 8;
        }
        else if ((lead & 0xF8U) == 0xF0U)
        {
            continuation_count = 3;
            minimum = 0x10000U >> 16;
        }
        else
        {
            return false;
        }

        if (index + continuation_count >= bytes.size())
        {
            return false;
        }

        std::uint32_t code_point = lead & ((1U << (7 - continuation_count - 1)) - 1U);
        for (std::size_t offset = 1; offset <= continuation_count; ++offset)
        {
            auto const continuation = std::to_integer<unsigned char>(bytes[index + offset]);
            if ((continuation & 0xC0U) != 0x80U)
            {
                return false;
            }

            code_point = (code_point << 6U) | (continuation & 0x3FU);
        }

        if ((continuation_count == 1 && code_point < 0x80U) ||
            (continuation_count == 2 && code_point < 0x800U) ||
            (continuation_count == 3 && code_point < 0x10000U) ||
            code_point > 0x10FFFFU ||
            (code_point >= 0xD800U && code_point <= 0xDFFFU))
        {
            return false;
        }

        index += continuation_count + 1;
    }

    return true;
}

} // namespace dearstory::protocol
