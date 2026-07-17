#pragma once

#include <dearstory/sdk/story_registration.hpp>

#define DEARSTORY_STORY(ID, FUNCTION, ARGS_TYPE) \
    ::dearstory::sdk::story_registration::create(ID, FUNCTION, ::dearstory::sdk::describe_arguments<ARGS_TYPE>())
