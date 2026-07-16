#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <span>
#include <string>
#include <vector>

#include <catch2/catch_test_macros.hpp>
#include <dearstory/protocol/framing.hpp>

namespace
{
    std::span<const std::byte> as_span(const std::vector<std::byte>& bytes)
    {
        return { bytes.data(), bytes.size() };
    }
}

TEST_CASE("protocol_framing decodes a frame split one byte at a time")
{
    const auto framed = dearstory::protocol::frame(R"({"type":"hello"})");
    dearstory::protocol::frame_decoder decoder;
    std::vector<std::string> frames;

    for (const auto byte : framed)
    {
        const auto result = decoder.push(std::span(&byte, std::size_t{ 1 }));
        REQUIRE(result.has_value());
        frames.insert(frames.end(), result.value().begin(), result.value().end());
    }

    REQUIRE(frames == std::vector<std::string>{ R"({"type":"hello"})" });
}

TEST_CASE("protocol_framing decodes two frames from one span")
{
    const auto first = dearstory::protocol::frame(R"({"type":"hello"})");
    const auto second = dearstory::protocol::frame(R"({"type":"welcome"})");
    auto combined = first;
    combined.insert(combined.end(), second.begin(), second.end());

    dearstory::protocol::frame_decoder decoder;
    const auto result = decoder.push(as_span(combined));
    REQUIRE(result.has_value());
    REQUIRE(result.value() == std::vector<std::string>{ R"({"type":"hello"})", R"({"type":"welcome"})" });
}

TEST_CASE("protocol_framing accepts a zero-length frame")
{
    const std::array<std::byte, 4> prefix{};
    dearstory::protocol::frame_decoder decoder;
    const auto result = decoder.push(std::span(prefix));
    REQUIRE(result.has_value());
    REQUIRE(result.value().size() == 1);
    REQUIRE(result.value().front().empty());
}

TEST_CASE("protocol_framing rejects invalid utf8")
{
    std::vector<std::byte> invalid_frame{
        std::byte{ 0x02 },
        std::byte{ 0x00 },
        std::byte{ 0x00 },
        std::byte{ 0x00 },
        std::byte{ 0xC3 },
        std::byte{ 0x28 }
    };

    dearstory::protocol::frame_decoder decoder;
    const auto result = decoder.push(as_span(invalid_frame));
    REQUIRE_FALSE(result.has_value());
    REQUIRE(result.error().code == "protocol.invalid_envelope");
}

TEST_CASE("protocol_framing rejects an oversized prefix before buffering payload")
{
    constexpr std::uint32_t oversize = dearstory::protocol::max_control_frame_bytes + 1;
    const auto prefix_value = std::bit_cast<std::array<std::byte, sizeof(std::uint32_t)>>(oversize);

    dearstory::protocol::frame_decoder decoder;
    const auto result = decoder.push(std::span(prefix_value));
    REQUIRE_FALSE(result.has_value());
    REQUIRE(result.error().code == "protocol.frame_too_large");
}
