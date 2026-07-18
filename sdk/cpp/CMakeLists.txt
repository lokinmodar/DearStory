add_library(
    dearstory_sdk_cpp
    src/story_registry.cpp)
add_library(DearStory::SdkCpp ALIAS dearstory_sdk_cpp)
set_target_properties(dearstory_sdk_cpp PROPERTIES EXPORT_NAME SdkCpp)

target_include_directories(
    dearstory_sdk_cpp
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>)

target_link_libraries(
    dearstory_sdk_cpp
    PUBLIC
        DearStory::CoreCpp)

target_compile_features(dearstory_sdk_cpp PUBLIC cxx_std_20)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_sdk_cpp)

if(MSVC)
    target_compile_options(
        dearstory_sdk_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()

install(
    TARGETS dearstory_sdk_cpp
    EXPORT DearStoryTargets
    ARCHIVE DESTINATION lib
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin)

install(DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/include/ DESTINATION include)
