#include <array>
#include <bit>
#include <chrono>
#include <future>
#include <memory>
#include <optional>
#include <string>
#include <vector>

#include <catch2/catch_test_macros.hpp>
#include <dearstory/protocol/control_envelope.hpp>
#include <dearstory/protocol/codec.hpp>
#include <dearstory/protocol/handshake.hpp>
#include <dearstory/protocol/windows/named_pipe_client.hpp>
#include <dearstory/protocol/windows/named_pipe_server.hpp>
#include <Windows.h>

namespace
{
    dearstory::protocol::generated::implementation_identity make_identity(std::string_view language)
    {
        return dearstory::protocol::generated::implementation_identity{
            .language = std::string(language),
            .name = "DearStory.Test",
            .toolchain = "test-toolchain",
            .version = "1.0.0"
        };
    }

    dearstory::protocol::control_envelope make_hello_envelope(
        dearstory::protocol::version protocol,
        std::string message_id,
        std::vector<std::string> supported,
        std::vector<std::string> required)
    {
        return dearstory::protocol::control_envelope{
            .protocol = protocol,
            .type = "hello",
            .message_id = std::move(message_id),
            .timestamp = "2026-07-16T00:00:00.000Z",
            .payload = dearstory::protocol::generated::hello{
                .implementation = make_identity("cpp"),
                .requiredCapabilities = std::move(required),
                .role = dearstory::protocol::generated::peer_role::host,
                .supportedCapabilities = std::move(supported)
            }
        };
    }

    dearstory::protocol::handshake_policy make_policy(
        dearstory::protocol::version local_version,
        std::vector<std::string> supported_capabilities)
    {
        auto uuids = std::make_shared<std::vector<std::string>>(std::vector<std::string>{
            "11111111-1111-4111-8111-111111111111",
            "22222222-2222-4222-8222-222222222222",
            "33333333-3333-4333-8333-333333333333"
        });

        return dearstory::protocol::handshake_policy{
            .local_version = local_version,
            .local_implementation = make_identity("cpp"),
            .supported_capabilities = std::move(supported_capabilities),
            .create_uuid = [uuids]() mutable {
                const auto value = uuids->front();
                uuids->erase(uuids->begin());
                return value;
            },
            .create_timestamp = [] { return std::string{ "2026-07-16T00:00:01.000Z" }; }
        };
    }

    std::wstring unique_pipe_name()
    {
        const auto ticks = std::chrono::steady_clock::now().time_since_epoch().count();
        return L"\\\\.\\pipe\\dearstory-test-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(ticks);
    }

    dearstory::protocol::control_envelope make_runtime_hello(std::string message_id)
    {
        return make_hello_envelope(
            dearstory::protocol::version{ 1, 0 },
            std::move(message_id),
            { "control.handshake.v1", "story.run" },
            { "control.handshake.v1" });
    }
}

TEST_CASE("protocol_handshake accepts protocol 1.0 and preserves correlation")
{
    auto policy = make_policy(
        dearstory::protocol::version{ 1, 0 },
        { "control.handshake.v1", "story.run", "visual.snapshot" });
    const auto hello = make_hello_envelope(
        dearstory::protocol::version{ 1, 0 },
        "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        { "visual.snapshot", "control.handshake.v1", "story.run" },
        { "control.handshake.v1" });

    const auto response = dearstory::protocol::negotiate(hello, policy);

    REQUIRE(response.type == "welcome");
    REQUIRE(response.protocol == dearstory::protocol::version{ 1, 0 });
    REQUIRE(response.message_id == "11111111-1111-4111-8111-111111111111");
    REQUIRE(response.correlation_id == std::optional<std::string>{ hello.message_id });
    REQUIRE(response.session_id == std::optional<std::string>{ "22222222-2222-4222-8222-222222222222" });
    REQUIRE(response.timestamp == "2026-07-16T00:00:01.000Z");

    const auto* welcome = std::get_if<dearstory::protocol::generated::welcome>(&response.payload);
    REQUIRE(welcome != nullptr);
    REQUIRE(welcome->peerId == "33333333-3333-4333-8333-333333333333");
    REQUIRE(welcome->acceptedCapabilities ==
        std::vector<std::string>{ "control.handshake.v1", "story.run", "visual.snapshot" });
}

TEST_CASE("protocol_handshake negotiates the lower minor when majors match")
{
    auto policy = make_policy(
        dearstory::protocol::version{ 1, 3 },
        { "control.handshake.v1", "story.run" });
    const auto hello = make_hello_envelope(
        dearstory::protocol::version{ 1, 1 },
        "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
        { "story.run", "control.handshake.v1" },
        { "control.handshake.v1" });

    const auto response = dearstory::protocol::negotiate(hello, policy);

    REQUIRE(response.type == "welcome");
    REQUIRE(response.protocol == dearstory::protocol::version{ 1, 1 });

    const auto* welcome = std::get_if<dearstory::protocol::generated::welcome>(&response.payload);
    REQUIRE(welcome != nullptr);
    REQUIRE(welcome->negotiatedVersion == dearstory::protocol::generated::protocol_version{ .major = 1, .minor = 1 });
}

TEST_CASE("protocol_handshake rejects a protocol major mismatch")
{
    auto policy = make_policy(
        dearstory::protocol::version{ 1, 0 },
        { "control.handshake.v1" });
    const auto hello = make_hello_envelope(
        dearstory::protocol::version{ 2, 0 },
        "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
        { "control.handshake.v1" },
        { "control.handshake.v1" });

    const auto response = dearstory::protocol::negotiate(hello, policy);

    REQUIRE(response.type == "reject");

    const auto* reject = std::get_if<dearstory::protocol::generated::reject>(&response.payload);
    REQUIRE(reject != nullptr);
    REQUIRE(reject->error.code == "protocol.major_mismatch");
}

TEST_CASE("protocol_handshake rejects a missing required capability")
{
    auto policy = make_policy(
        dearstory::protocol::version{ 1, 0 },
        { "control.handshake.v1" });
    const auto hello = make_hello_envelope(
        dearstory::protocol::version{ 1, 0 },
        "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
        { "control.handshake.v1", "story.run" },
        { "control.handshake.v1", "visual.snapshot" });

    const auto response = dearstory::protocol::negotiate(hello, policy);

    REQUIRE(response.type == "reject");

    const auto* reject = std::get_if<dearstory::protocol::generated::reject>(&response.payload);
    REQUIRE(reject != nullptr);
    REQUIRE(reject->error.code == "protocol.required_capability_missing");
}

TEST_CASE("protocol_handshake rejects duplicate capabilities")
{
    auto policy = make_policy(
        dearstory::protocol::version{ 1, 0 },
        { "control.handshake.v1", "story.run" });
    const auto hello = make_hello_envelope(
        dearstory::protocol::version{ 1, 0 },
        "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee",
        { "control.handshake.v1", "story.run", "story.run" },
        { "control.handshake.v1" });

    const auto response = dearstory::protocol::negotiate(hello, policy);

    REQUIRE(response.type == "reject");

    const auto* reject = std::get_if<dearstory::protocol::generated::reject>(&response.payload);
    REQUIRE(reject != nullptr);
    REQUIRE(reject->error.code == "protocol.invalid_envelope");
}

TEST_CASE("protocol_pipe exchanges two framed messages in order")
{
    auto const pipe_name = unique_pipe_name();
    dearstory::protocol::windows::named_pipe_server server(pipe_name);
    auto accepted = std::async(std::launch::async, [&server] {
        return server.accept(std::stop_token{});
    });

    auto client = dearstory::protocol::windows::named_pipe_client::connect(pipe_name, std::stop_token{});
    auto server_connection = accepted.get();

    const auto first = dearstory::protocol::encode(make_runtime_hello("01010101-0101-4101-8101-010101010101"));
    const auto second = dearstory::protocol::encode(make_runtime_hello("02020202-0202-4202-8202-020202020202"));
    const auto direct_first = dearstory::protocol::decode(first);
    const auto direct_second = dearstory::protocol::decode(second);
    REQUIRE(direct_first.has_value());
    REQUIRE(direct_second.has_value());

    auto writer = std::async(std::launch::async, [&] {
        client.write_payload(first);
        client.write_payload(second);
    });

    const auto first_frame = server_connection.read_payload();
    const auto second_frame = server_connection.read_payload();
    writer.get();

    REQUIRE(first_frame.has_value());
    REQUIRE(second_frame.has_value());
    REQUIRE(*first_frame == first);
    REQUIRE(*second_frame == second);

    const auto first_decoded = dearstory::protocol::decode(*first_frame);
    const auto second_decoded = dearstory::protocol::decode(*second_frame);
    REQUIRE(first_decoded.has_value());
    REQUIRE(second_decoded.has_value());
    REQUIRE(first_decoded->message_id == "01010101-0101-4101-8101-010101010101");
    REQUIRE(second_decoded->message_id == "02020202-0202-4202-8202-020202020202");
}

TEST_CASE("protocol_pipe can cancel an accept before a client connects")
{
    auto const pipe_name = unique_pipe_name();
    dearstory::protocol::windows::named_pipe_server server(pipe_name);
    std::stop_source stop_source;
    stop_source.request_stop();

    REQUIRE_THROWS(server.accept(stop_source.get_token()));
}

TEST_CASE("protocol_pipe rejects a peer disconnect in the middle of a frame")
{
    auto const pipe_name = unique_pipe_name();
    dearstory::protocol::windows::named_pipe_server server(pipe_name);
    auto accepted = std::async(std::launch::async, [&server] {
        return server.accept(std::stop_token{});
    });

    const auto raw = CreateFileW(
        pipe_name.c_str(),
        GENERIC_READ | GENERIC_WRITE,
        0,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    REQUIRE(raw != INVALID_HANDLE_VALUE);

    auto server_connection = accepted.get();
    auto read_result = std::async(std::launch::async, [&] {
        return server_connection.read_payload();
    });

    const std::uint32_t payload_length = 32;
    const auto prefix = std::bit_cast<std::array<std::byte, sizeof(std::uint32_t)>>(payload_length);
    DWORD bytes_written = 0;
    REQUIRE(WriteFile(raw, prefix.data(), static_cast<DWORD>(prefix.size()), &bytes_written, nullptr));
    REQUIRE(bytes_written == prefix.size());

    const char partial_payload[] = "{\"type\":\"hello\"";
    REQUIRE(WriteFile(raw, partial_payload, static_cast<DWORD>(sizeof(partial_payload) - 1), &bytes_written, nullptr));
    CloseHandle(raw);

    REQUIRE_THROWS(read_result.get());
}

TEST_CASE("protocol_pipe rejects a second client while the first holds the only server instance")
{
    auto const pipe_name = unique_pipe_name();
    dearstory::protocol::windows::named_pipe_server server(pipe_name);
    auto accepted = std::async(std::launch::async, [&server] {
        return server.accept(std::stop_token{});
    });

    auto first_client = dearstory::protocol::windows::named_pipe_client::connect(pipe_name, std::stop_token{});
    auto server_connection = accepted.get();

    std::stop_source stop_source;
    auto second_connect = std::async(std::launch::async, [&] {
        return dearstory::protocol::windows::named_pipe_client::connect(pipe_name, stop_source.get_token());
    });

    stop_source.request_stop();
    REQUIRE_THROWS(second_connect.get());

    (void)server_connection;
    (void)first_client;
}
