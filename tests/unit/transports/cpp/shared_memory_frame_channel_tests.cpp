#include <catch2/catch_test_macros.hpp>
#include <dearstory/transports/windows/shared_memory_frame_channel.hpp>

#include <array>
#include <cstddef>

TEST_CASE("shared memory frame channel publishes monotonic sequence")
{
    auto const descriptor = dearstory::transports::windows::frame_transport_descriptor::create(
        L"Local\\dearstory-frame-test-cpp",
        2,
        2,
        8,
        3);

    dearstory::transports::windows::shared_memory_frame_channel channel(descriptor);

    std::array<std::byte, 16> pixels{};
    auto const first = channel.publish(pixels);
    auto const second = channel.publish(pixels);

    REQUIRE(first.sequence == 1);
    REQUIRE(second.sequence == 2);
}
