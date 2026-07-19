find_package(nlohmann_json CONFIG REQUIRED)

add_library(
    dearstory_protocol_cpp
    src/handshake.cpp
    src/codec.cpp
    src/framing.cpp)
add_library(DearStory::ProtocolCpp ALIAS dearstory_protocol_cpp)
set_target_properties(dearstory_protocol_cpp PROPERTIES EXPORT_NAME ProtocolCpp)

target_include_directories(
    dearstory_protocol_cpp
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>)

target_link_libraries(
    dearstory_protocol_cpp
    PUBLIC
        nlohmann_json::nlohmann_json)

target_compile_features(dearstory_protocol_cpp PUBLIC cxx_std_20)

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory_protocol_cpp)

if(MSVC)
    target_compile_options(
        dearstory_protocol_cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
endif()

install(
    TARGETS dearstory_protocol_cpp
    EXPORT DearStoryTargets
    ARCHIVE DESTINATION lib
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin)

install(
    DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/include/
    DESTINATION include
    PATTERN "windows" EXCLUDE)
