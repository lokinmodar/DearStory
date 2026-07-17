#pragma once

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <span>
#include <string>

namespace dearstory::transports::windows
{
/// Describes one Windows shared-memory RGBA frame channel.
struct frame_transport_descriptor final
{
    /// The Windows file-mapping name.
    std::wstring mapping_name{};
    /// The frame width in pixels.
    std::int32_t width{};
    /// The frame height in pixels.
    std::int32_t height{};
    /// The row stride in bytes.
    std::int32_t stride{};
    /// The number of frame slots in the ring.
    std::int32_t slot_count{};

    /// Creates one validated transport descriptor.
    [[nodiscard]] static frame_transport_descriptor create(
        std::wstring mapping_name,
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::int32_t slot_count);

    /// Gets the byte length of one RGBA frame.
    [[nodiscard]] std::int32_t frame_byte_length() const noexcept;
    /// Gets the byte length of one slot including metadata.
    [[nodiscard]] std::int32_t slot_byte_length() const noexcept;
    /// Gets the total byte length of the mapping.
    [[nodiscard]] std::uint64_t total_byte_length() const noexcept;
};

/// Describes one published frame.
struct published_frame final
{
    /// The source slot index.
    std::int32_t slot_index{};
    /// The monotonic frame sequence.
    std::int64_t sequence{};
    /// The UTC publication timestamp.
    std::chrono::sys_time<std::chrono::milliseconds> timestamp_utc{};
};

/// Publishes RGBA frames into one Windows shared-memory mapping.
class shared_memory_frame_channel final
{
public:
    /// Opens one frame channel for the supplied descriptor.
    explicit shared_memory_frame_channel(frame_transport_descriptor descriptor);
    ~shared_memory_frame_channel();

    shared_memory_frame_channel(shared_memory_frame_channel const&) = delete;
    shared_memory_frame_channel& operator=(shared_memory_frame_channel const&) = delete;

    /// Publishes one full RGBA frame into the next slot.
    [[nodiscard]] published_frame publish(std::span<std::byte const> rgba_bytes);

private:
    frame_transport_descriptor descriptor_;
    void* mapping_handle_{};
    std::byte* mapped_view_{};
    std::int64_t sequence_{};
};
}
