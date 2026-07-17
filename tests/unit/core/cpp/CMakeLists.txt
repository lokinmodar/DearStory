find_package(Catch2 CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)

add_executable(
    dearstory-core-cpp-tests
    story_id_tests.cpp
    story_catalog_tests.cpp
    story_session_tests.cpp)

target_link_libraries(
    dearstory-core-cpp-tests
    PRIVATE
        Catch2::Catch2WithMain
        DearStory::CoreCpp
        nlohmann_json::nlohmann_json)

set_target_properties(
    dearstory-core-cpp-tests
    PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/artifacts/bin/native/$<CONFIG>")

if(MSVC)
    target_compile_options(
        dearstory-core-cpp-tests
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
    target_link_options(
        dearstory-core-cpp-tests
        PRIVATE
            "$<$<CONFIG:Release>:/DEBUG:FULL>")
endif()

include(Catch)
catch_discover_tests(dearstory-core-cpp-tests)
