cmake_minimum_required(VERSION 3.30)

file(READ "${CMAKE_SOURCE_DIR}/eng/version.json" DEARSTORY_VERSION_JSON)
string(JSON DEARSTORY_VERSION ERROR_VARIABLE DEARSTORY_VERSION_ERROR GET "${DEARSTORY_VERSION_JSON}" version)
if(DEARSTORY_VERSION_ERROR)
    message(FATAL_ERROR "Failed to read DearStory version from eng/version.json: ${DEARSTORY_VERSION_ERROR}")
endif()

project(DearStory VERSION ${DEARSTORY_VERSION} LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(CTest)
include(CMakePackageConfigHelpers)

configure_package_config_file(
    ${CMAKE_SOURCE_DIR}/cmake/DearStoryConfig.cmake.in
    ${CMAKE_BINARY_DIR}/DearStoryConfig.cmake
    INSTALL_DESTINATION lib/cmake/DearStory)

write_basic_package_version_file(
    ${CMAKE_BINARY_DIR}/DearStoryConfigVersion.cmake
    VERSION ${PROJECT_VERSION}
    COMPATIBILITY ExactVersion)

add_subdirectory(src/protocol/cpp)
add_subdirectory(src/core/cpp)
add_subdirectory(src/transports/cpp)
add_subdirectory(src/hosts/cpp)
add_subdirectory(sdk/cpp)
add_subdirectory(tools/DearStory.ProtocolProbe.Cpp)

if(BUILD_TESTING)
    add_subdirectory(tests/unit/core/cpp)
    add_subdirectory(tests/unit/protocol/cpp)
    add_subdirectory(tests/unit/sdk/cpp)
    add_subdirectory(tests/unit/transports/cpp)
endif()

install(
    EXPORT DearStoryTargets
    FILE DearStoryTargets.cmake
    NAMESPACE DearStory::
    DESTINATION lib/cmake/DearStory)

install(
    FILES
        ${CMAKE_BINARY_DIR}/DearStoryConfig.cmake
        ${CMAKE_BINARY_DIR}/DearStoryConfigVersion.cmake
    DESTINATION lib/cmake/DearStory)

install(
    FILES ${CMAKE_SOURCE_DIR}/LICENSE
    DESTINATION share/licenses/DearStory)
