find_package(Catch2 CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)

add_executable(
    dearstory-sdk-cpp-tests
    argument_descriptor_tests.cpp
    story_registry_tests.cpp)

target_link_libraries(
    dearstory-sdk-cpp-tests
    PRIVATE
        Catch2::Catch2WithMain
        DearStory::SdkCpp
        nlohmann_json::nlohmann_json)

set_target_properties(
    dearstory-sdk-cpp-tests
    PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/artifacts/bin/native/$<CONFIG>")

if(MSVC)
    target_compile_options(
        dearstory-sdk-cpp-tests
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
    target_link_options(
        dearstory-sdk-cpp-tests
        PRIVATE
            "$<$<CONFIG:Release>:/DEBUG:FULL>")
endif()

include(Catch)
catch_discover_tests(dearstory-sdk-cpp-tests)
