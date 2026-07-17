#include <dearstory/transports/windows/shared_memory_frame_channel.hpp>

#include <windows.h>

#include <chrono>
#include <cstring>
#include <stdexcept>
#include <utility>

namespace dearstory::transports::windows
{
    namespace
    {
        constexpr std::int32_t sequence_offset = 0;
        constexpr std::int32_t payload_length_offset = 8;
        constexpr std::int32_t payload_offset = 16;
    }

    frame_transport_descriptor frame_transport_descriptor::create(
        std::wstring mapping_name,
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::int32_t slot_count)
    {
        if (mapping_name.empty())
        {
            throw std::invalid_argument("The mapping name must be provided.");
        }

        if (width <= 0 || height <= 0)
        {
            throw std::invalid_argument("The frame width and height must be positive.");
        }

        if (stride < width * 4)
        {
            throw std::invalid_argument("The frame stride must be at least width * 4 bytes for RGBA8 pixels.");
        }

        if (slot_count <= 0)
        {
            throw std::invalid_argument("The slot count must be positive.");
        }

        return frame_transport_descriptor{
            .mapping_name = std::move(mapping_name),
            .width = width,
            .height = height,
            .stride = stride,
            .slot_count = slot_count
        };
    }

    std::int32_t frame_transport_descriptor::frame_byte_length() const noexcept
    {
        return height * stride;
    }

    std::int32_t frame_transport_descriptor::slot_byte_length() const noexcept
    {
        return payload_offset + frame_byte_length();
    }

    std::uint64_t frame_transport_descriptor::total_byte_length() const noexcept
    {
        return static_cast<std::uint64_t>(slot_count) * static_cast<std::uint64_t>(slot_byte_length());
    }

    shared_memory_frame_channel::shared_memory_frame_channel(frame_transport_descriptor descriptor)
        : descriptor_(std::move(descriptor))
    {
        auto const total_byte_length = descriptor_.total_byte_length();
        auto const maximum_size_high = static_cast<DWORD>(total_byte_length >> 32);
        auto const maximum_size_low = static_cast<DWORD>(total_byte_length & 0xFFFFFFFFull);

        mapping_handle_ = CreateFileMappingW(
            INVALID_HANDLE_VALUE,
            nullptr,
            PAGE_READWRITE,
            maximum_size_high,
            maximum_size_low,
            descriptor_.mapping_name.c_str());

        if (mapping_handle_ == nullptr)
        {
            throw std::runtime_error("CreateFileMappingW failed.");
        }

        mapped_view_ = static_cast<std::byte*>(MapViewOfFile(
            mapping_handle_,
            FILE_MAP_ALL_ACCESS,
            0,
            0,
            static_cast<SIZE_T>(total_byte_length)));

        if (mapped_view_ == nullptr)
        {
            CloseHandle(mapping_handle_);
            mapping_handle_ = nullptr;
            throw std::runtime_error("MapViewOfFile failed.");
        }
    }

    shared_memory_frame_channel::~shared_memory_frame_channel()
    {
        if (mapped_view_ != nullptr)
        {
            UnmapViewOfFile(mapped_view_);
            mapped_view_ = nullptr;
        }

        if (mapping_handle_ != nullptr)
        {
            CloseHandle(mapping_handle_);
            mapping_handle_ = nullptr;
        }
    }

    published_frame shared_memory_frame_channel::publish(std::span<std::byte const> rgba_bytes)
    {
        if (rgba_bytes.size() != static_cast<std::size_t>(descriptor_.frame_byte_length()))
        {
            throw std::invalid_argument("The RGBA payload must match the configured frame byte length.");
        }

        auto const sequence = ++sequence_;
        auto const slot_index = static_cast<std::int32_t>((sequence - 1) % descriptor_.slot_count);
        auto const slot_offset = static_cast<std::size_t>(slot_index) * static_cast<std::size_t>(descriptor_.slot_byte_length());
        auto* const slot = mapped_view_ + slot_offset;
        auto const payload_length = static_cast<std::int32_t>(rgba_bytes.size());
        auto const zero_sequence = std::int64_t{ 0 };

        std::memcpy(slot + payload_length_offset, &payload_length, sizeof(payload_length));
        std::memcpy(slot + payload_offset, rgba_bytes.data(), rgba_bytes.size_bytes());
        FlushViewOfFile(slot, static_cast<SIZE_T>(descriptor_.slot_byte_length()));
        std::memcpy(slot + sequence_offset, &zero_sequence, sizeof(zero_sequence));
        std::memcpy(slot + sequence_offset, &sequence, sizeof(sequence));
        FlushViewOfFile(slot, static_cast<SIZE_T>(descriptor_.slot_byte_length()));

        return published_frame{
            .slot_index = slot_index,
            .sequence = sequence,
            .timestamp_utc = std::chrono::time_point_cast<std::chrono::milliseconds>(std::chrono::system_clock::now())
        };
    }
}
