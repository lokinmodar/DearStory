#include <dearstory/protocol/version.hpp>
#include <catch2/catch_test_macros.hpp>

TEST_CASE("protocol 1.0 negotiates the lower compatible minor")
{
    using dearstory::protocol::version;
    REQUIRE(version{1, 0}.negotiate(version{1, 3}) == version{1, 0});
    REQUIRE_FALSE(version{2, 0}.is_major_compatible(version{1, 0}));
}
