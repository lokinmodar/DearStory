find_package(nlohmann_json CONFIG REQUIRED)

add_library(
    dearstory_core_cpp
    src/argument_patch_validator.cpp
    src/story_id.cpp
    src/story_catalog.cpp
    src/story_session.cpp)
add_library(DearStory::CoreCpp ALIAS dearstory_core_cpp)
set_target_properties(dearstory_core_cpp PROPERTIES EXPORT_NAME CoreCpp)

target_include_directories(
    dearstory_core_cpp
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>)

target_link_libraries(
    dearstory_core_cpp
    PUBLIC
        DearStory::ProtocolCpp
        nlohmann_json::nlohmann_json)

target_compile_features(dearstory_core_cpp PUBLIC cxx_std_20)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_core_cpp)

if(MSVC)
    target_compile_options(
        dearstory_core_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()

install(
    TARGETS dearstory_core_cpp
    EXPORT DearStoryTargets
    ARCHIVE DESTINATION lib
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin)

install(DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/include/ DESTINATION include)
