include(FetchContent)
find_package(nlohmann_json CONFIG REQUIRED)

set(DEARSTORY_IMGUI_GIT_TAG 8936b58fe26e8c3da834b8f60b06511d537b4c63)
FetchContent_Declare(
    imgui
    GIT_REPOSITORY https://github.com/ocornut/imgui.git
    GIT_TAG ${DEARSTORY_IMGUI_GIT_TAG}
    GIT_SHALLOW TRUE)
FetchContent_MakeAvailable(imgui)

add_library(
    dearstory_imgui
    STATIC
        ${imgui_SOURCE_DIR}/imgui.cpp
        ${imgui_SOURCE_DIR}/imgui_draw.cpp
        ${imgui_SOURCE_DIR}/imgui_tables.cpp
        ${imgui_SOURCE_DIR}/imgui_widgets.cpp)

target_include_directories(
    dearstory_imgui
    PUBLIC
        ${imgui_SOURCE_DIR})

add_executable(
    dearstory-host-cpp
    src/main.cpp
    src/native_host.cpp
    ${CMAKE_SOURCE_DIR}/examples/workspaces/windows-slice/cpp/src/buttons_primary.cpp)

add_library(dearstory_hosts_cpp INTERFACE)
add_library(DearStory::HostsCpp ALIAS dearstory_hosts_cpp)

target_include_directories(
    dearstory-host-cpp
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include)

target_link_libraries(
    dearstory-host-cpp
    PRIVATE
        dearstory_imgui
        DearStory::CoreCpp
        DearStory::ProtocolCpp
        DearStory::SdkCpp
        DearStory::TransportsCpp
        nlohmann_json::nlohmann_json
        Rpcrt4)

set_target_properties(
    dearstory-host-cpp
    PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/artifacts/bin/native/$<CONFIG>")

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory-host-cpp)

if(MSVC)
    target_compile_options(
        dearstory-host-cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
    target_link_options(
        dearstory-host-cpp
        PRIVATE
            "$<$<CONFIG:Release>:/DEBUG:FULL>")
endif()
