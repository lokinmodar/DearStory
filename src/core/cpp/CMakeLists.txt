find_package(nlohmann_json CONFIG REQUIRED)

add_library(
    dearstory_core_cpp
    src/argument_patch_validator.cpp
    src/story_id.cpp
    src/story_catalog.cpp
    src/story_session.cpp)
add_library(DearStory::CoreCpp ALIAS dearstory_core_cpp)

target_include_directories(
    dearstory_core_cpp
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include)

target_link_libraries(
    dearstory_core_cpp
    PUBLIC
        DearStory::ProtocolCpp
        nlohmann_json::nlohmann_json)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_core_cpp)

if(MSVC)
    target_compile_options(
        dearstory_core_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()
