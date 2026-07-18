#include <dearstory/sdk/story_registry.hpp>

int main()
{
    dearstory::sdk::story_registry registry;
    return registry.registrations().empty() ? 0 : 1;
}
