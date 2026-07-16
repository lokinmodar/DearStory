#include <variant>

#include <catch2/catch_test_macros.hpp>
#include <dearstory/protocol/codec.hpp>

#include "test_vectors.hpp"

TEST_CASE("protocol_codec round-trips the canonical hello vector")
{
    const auto json = read_vector("hello.valid.json");
    const auto decoded = dearstory::protocol::decode(json);
    REQUIRE(decoded.has_value());
    REQUIRE(decoded->type == "hello");
    REQUIRE(std::holds_alternative<dearstory::protocol::generated::hello>(decoded->payload));
    REQUIRE(json_semantically_equal(encode(*decoded), json));
}

TEST_CASE("protocol_codec rejects an envelope without messageId")
{
    const auto decoded = dearstory::protocol::decode(read_vector("hello.missing-message-id.json"));
    REQUIRE_FALSE(decoded.has_value());
    REQUIRE(decoded.error().code == "protocol.invalid_envelope");
}

TEST_CASE("protocol_codec preserves a higher protocol major for handshake rejection")
{
    const auto decoded = dearstory::protocol::decode(
        R"({"protocol":{"major":2,"minor":0},"type":"hello","messageId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00.000Z","payload":{"role":"host","implementation":{"name":"DearStory.Test","version":"1.0.0","language":"cpp","toolchain":"test-toolchain"},"supportedCapabilities":["control.handshake.v1"],"requiredCapabilities":["control.handshake.v1"]}})");

    REQUIRE(decoded.has_value());
    REQUIRE(decoded->protocol == dearstory::protocol::version{ 2, 0 });
}

TEST_CASE("protocol_codec round-trips a welcome envelope with correlation metadata")
{
    const auto json =
        R"({"protocol":{"major":1,"minor":0},"type":"welcome","messageId":"22222222-2222-4222-8222-222222222222","correlationId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00.100Z","payload":{"peerId":"33333333-3333-4333-8333-333333333333","negotiatedVersion":{"major":1,"minor":0},"acceptedCapabilities":["control.handshake.v1"]}})";

    const auto decoded = dearstory::protocol::decode(json);
    REQUIRE(decoded.has_value());
    REQUIRE(decoded->type == "welcome");
    REQUIRE(std::holds_alternative<dearstory::protocol::generated::welcome>(decoded->payload));
    REQUIRE(json_semantically_equal(encode(*decoded), json));
}

TEST_CASE("protocol_codec round-trips a reject envelope with details and session metadata")
{
    const auto json =
        R"({"protocol":{"major":1,"minor":0},"type":"reject","messageId":"22222222-2222-4222-8222-222222222222","sessionId":"33333333-3333-4333-8333-333333333333","timestamp":"2026-07-16T00:00:00.100Z","payload":{"error":{"code":"protocol.required_capability_missing","message":"Missing capability.","recovery":"Retry with control.handshake.v1.","details":{"missingCapability":"control.handshake.v1"}}}})";

    const auto decoded = dearstory::protocol::decode(json);
    REQUIRE(decoded.has_value());
    REQUIRE(decoded->type == "reject");
    REQUIRE(std::holds_alternative<dearstory::protocol::generated::reject>(decoded->payload));
    REQUIRE(json_semantically_equal(encode(*decoded), json));
}

TEST_CASE("protocol_codec rejects an invalid timestamp shape")
{
    const auto decoded = dearstory::protocol::decode(
        R"({"protocol":{"major":1,"minor":0},"type":"hello","messageId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00Z","payload":{"role":"host","implementation":{"name":"DearStory.Test","version":"1.0.0","language":"cpp","toolchain":"test-toolchain"},"supportedCapabilities":["control.handshake.v1"],"requiredCapabilities":["control.handshake.v1"]}})");

    REQUIRE_FALSE(decoded.has_value());
    REQUIRE(decoded.error().code == "protocol.invalid_envelope");
}

TEST_CASE("protocol_codec rejects an invalid peer role")
{
    const auto decoded = dearstory::protocol::decode(
        R"({"protocol":{"major":1,"minor":0},"type":"hello","messageId":"11111111-1111-4111-8111-111111111111","timestamp":"2026-07-16T00:00:00.000Z","payload":{"role":"invalid","implementation":{"name":"DearStory.Test","version":"1.0.0","language":"cpp","toolchain":"test-toolchain"},"supportedCapabilities":["control.handshake.v1"],"requiredCapabilities":["control.handshake.v1"]}})");

    REQUIRE_FALSE(decoded.has_value());
    REQUIRE(decoded.error().code == "protocol.invalid_envelope");
}
