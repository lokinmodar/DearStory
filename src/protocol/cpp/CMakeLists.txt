find_package(nlohmann_json CONFIG REQUIRED)

add_library(
    dearstory_protocol_cpp
    src/handshake.cpp
    src/codec.cpp
    src/framing.cpp
    src/windows/named_pipe_client.cpp
    src/windows/named_pipe_server.cpp)
add_library(DearStory::ProtocolCpp ALIAS dearstory_protocol_cpp)

target_include_directories(
    dearstory_protocol_cpp
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include)

target_link_libraries(
    dearstory_protocol_cpp
    PUBLIC
        nlohmann_json::nlohmann_json)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_protocol_cpp)

if(MSVC)
    target_compile_options(
        dearstory_protocol_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()
