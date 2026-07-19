add_library(
    dearstory_transports_cpp
    src/named_pipe_client.cpp
    src/named_pipe_server.cpp
    src/shared_memory_frame_channel.cpp)
add_library(DearStory::TransportsCpp ALIAS dearstory_transports_cpp)

target_include_directories(
    dearstory_transports_cpp
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include)

target_link_libraries(
    dearstory_transports_cpp
    PUBLIC
        DearStory::ProtocolCpp)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_transports_cpp)

if(MSVC)
    target_compile_options(
        dearstory_transports_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()
