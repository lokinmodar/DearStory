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
