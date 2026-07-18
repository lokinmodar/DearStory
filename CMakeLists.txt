cmake_minimum_required(VERSION 3.30)

project(DearStory VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(CTest)
include(CMakePackageConfigHelpers)

configure_package_config_file(
    ${CMAKE_SOURCE_DIR}/cmake/DearStoryConfig.cmake.in
    ${CMAKE_BINARY_DIR}/DearStoryConfig.cmake
    INSTALL_DESTINATION lib/cmake/DearStory)

configure_file(
    ${CMAKE_SOURCE_DIR}/cmake/DearStoryConfigVersion.cmake.in
    ${CMAKE_BINARY_DIR}/DearStoryConfigVersion.cmake
    @ONLY)

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
