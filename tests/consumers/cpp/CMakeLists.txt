cmake_minimum_required(VERSION 3.30)
project(dearstory_consumer_smoke LANGUAGES CXX)

include(CTest)

find_package(DearStory CONFIG REQUIRED)

add_executable(dearstory-consumer-smoke main.cpp package_consumption_tests.cpp)
target_link_libraries(dearstory-consumer-smoke PRIVATE DearStory::SdkCpp)

add_test(NAME dearstory-consumer-smoke COMMAND dearstory-consumer-smoke)
