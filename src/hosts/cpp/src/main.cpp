#include <dearstory/hosts/cpp/native_host.hpp>

#include <stdexcept>
#include <string>

namespace
{
    constexpr int exit_success = 0;
    constexpr int exit_usage = 64;
    constexpr int exit_runtime = 70;

    struct usage_error final : std::runtime_error {
        using std::runtime_error::runtime_error;
    };

    [[nodiscard]] dearstory::hosts::cpp::native_host_options parse_arguments(int argc, char** argv)
    {
        dearstory::hosts::cpp::native_host_options options{};

        for (int index = 1; index < argc; ++index)
        {
            std::string argument = argv[index];
            auto require_value = [&](char const* name) -> std::string {
                if (index + 1 >= argc)
                {
                    throw usage_error(std::string("Missing value for argument ") + name + ".");
                }

                return argv[++index];
            };

            if (argument == "--pipe")
            {
                options.pipe_name = require_value("--pipe");
            }
            else if (argument == "--host-id")
            {
                options.host_id = require_value("--host-id");
            }
            else if (argument == "--help" || argument == "-h" || argument == "/?")
            {
                throw usage_error("Usage: dearstory-host-cpp --pipe <name> --host-id <id>");
            }
            else
            {
                throw usage_error("Unknown argument: " + argument);
            }
        }

        if (options.pipe_name.empty())
        {
            throw usage_error("The --pipe argument is required.");
        }

        if (options.host_id.empty())
        {
            throw usage_error("The --host-id argument is required.");
        }

        return options;
    }
} // namespace

int main(int argc, char** argv)
{
    try
    {
        dearstory::hosts::cpp::native_host host(parse_arguments(argc, argv));
        return host.run();
    }
    catch (usage_error const&)
    {
        return exit_usage;
    }
    catch (...)
    {
        return exit_runtime;
    }
}
