find_package(nlohmann_json CONFIG REQUIRED)

add_library(dearstory_protocol_cpp INTERFACE)
add_library(DearStory::ProtocolCpp ALIAS dearstory_protocol_cpp)

target_include_directories(
    dearstory_protocol_cpp
    INTERFACE
        ${CMAKE_CURRENT_SOURCE_DIR}/include)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_protocol_cpp)
