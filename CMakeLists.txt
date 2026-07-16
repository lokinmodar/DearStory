cmake_minimum_required(VERSION 3.30)

project(DearStory VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

include(CTest)

add_subdirectory(src/protocol/cpp)

if(BUILD_TESTING)
    add_subdirectory(tests/unit/protocol/cpp)
endif()
