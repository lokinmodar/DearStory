add_executable(
    dearstory-protocol-probe-cpp
    main.cpp)

target_link_libraries(
    dearstory-protocol-probe-cpp
    PRIVATE
        DearStory::ProtocolCpp
        Rpcrt4)

set_target_properties(
    dearstory-protocol-probe-cpp
    PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/artifacts/bin/native/$<CONFIG>")

include(${CMAKE_SOURCE_DIR}/cmake/dearstory_warnings.cmake)
dearstory_enable_warnings(dearstory-protocol-probe-cpp)

if(MSVC)
    target_compile_options(
        dearstory-protocol-probe-cpp
        PRIVATE
            "$<$<CONFIG:Release>:/Zi>")
    target_link_options(
        dearstory-protocol-probe-cpp
        PRIVATE
            "$<$<CONFIG:Release>:/DEBUG:FULL>")
endif()
