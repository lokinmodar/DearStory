find_package(Catch2 CONFIG REQUIRED)

add_executable(
    dearstory-protocol-cpp-tests
    version_tests.cpp)

target_link_libraries(
    dearstory-protocol-cpp-tests
    PRIVATE
        Catch2::Catch2WithMain
        DearStory::ProtocolCpp)

set_target_properties(
    dearstory-protocol-cpp-tests
    PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/artifacts/bin/native/$<CONFIG>")

include(Catch)
catch_discover_tests(dearstory-protocol-cpp-tests)
