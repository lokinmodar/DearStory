add_library(
    dearstory_sdk_cpp
    src/story_registry.cpp)
add_library(DearStory::SdkCpp ALIAS dearstory_sdk_cpp)

target_include_directories(
    dearstory_sdk_cpp
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include)

target_link_libraries(
    dearstory_sdk_cpp
    PUBLIC
        DearStory::CoreCpp)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_sdk_cpp)

if(MSVC)
    target_compile_options(
        dearstory_sdk_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()
