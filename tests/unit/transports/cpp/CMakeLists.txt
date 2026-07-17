find_package(Catch2 CONFIG REQUIRED)

add_executable(
    dearstory-transports-cpp-tests
    shared_memory_frame_channel_tests.cpp)

target_link_libraries(
    dearstory-transports-cpp-tests
    PRIVATE
        Catch2::Catch2WithMain
        DearStory::TransportsCpp)

set_target_properties(
    dearstory-transports-cpp-tests
    PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/artifacts/bin/native/$<CONFIG>")

if(MSVC)
    target_compile_options(
        dearstory-transports-cpp-tests
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
    target_link_options(
        dearstory-transports-cpp-tests
        PRIVATE
            "$<$<CONFIG:Release>:/DEBUG:FULL>")
endif()

include(Catch)
catch_discover_tests(dearstory-transports-cpp-tests)
