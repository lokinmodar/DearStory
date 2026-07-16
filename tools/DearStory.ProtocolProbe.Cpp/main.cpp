#define NOMINMAX
#include <Windows.h>

#include <chrono>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>
#include <thread>
#include <utility>
#include <vector>

#include <nlohmann/json.hpp>

#include <dearstory/protocol/codec.hpp>
#include <dearstory/protocol/generated/messages.hpp>
#include <dearstory/protocol/handshake.hpp>
#include <dearstory/protocol/windows/named_pipe_client.hpp>
#include <dearstory/protocol/windows/named_pipe_server.hpp>

namespace
{
    constexpr int exit_success = 0;
    constexpr int exit_usage = 20;
    constexpr int exit_pipe = 21;
    constexpr int exit_protocol = 22;
    constexpr int exit_timeout = 23;

    struct options final {
        std::string mode{};
        std::string pipe{};
        std::string role{ "host" };
        std::vector<std::string> required_capabilities{};
        std::uint16_t protocol_major{ dearstory::protocol::current_major };
        std::uint16_t protocol_minor{ dearstory::protocol::current_minor };
    };

    struct usage_error final : std::runtime_error {
        using std::runtime_error::runtime_error;
    };

    [[nodiscard]] std::wstring to_wide(std::string_view value)
    {
        if (value.empty())
        {
            return {};
        }

        auto const size = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), nullptr, 0);
        if (size <= 0)
        {
            throw usage_error("Pipe names must be valid UTF-8.");
        }

        std::wstring wide(static_cast<std::size_t>(size), L'\0');
        (void)MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), wide.data(), size);
        return wide;
    }

    [[nodiscard]] std::wstring normalize_pipe_path(std::string_view value)
    {
        auto wide = to_wide(value);
        static constexpr std::wstring_view prefix = LR"(\\.\pipe\)";
        if (wide.rfind(prefix, 0) == 0)
        {
            return wide;
        }

        return std::wstring(prefix) + wide;
    }

    [[nodiscard]] dearstory::protocol::generated::peer_role parse_role(std::string_view role)
    {
        using dearstory::protocol::generated::peer_role;
        if (role == "runner")
        {
            return peer_role::runner;
        }

        if (role == "catalog")
        {
            return peer_role::catalog;
        }

        if (role == "host")
        {
            return peer_role::host;
        }

        throw usage_error("Role must be one of: runner, catalog, host.");
    }

    [[nodiscard]] std::string role_to_string(dearstory::protocol::generated::peer_role role)
    {
        using dearstory::protocol::generated::peer_role;
        switch (role)
        {
        case peer_role::runner:
            return "runner";
        case peer_role::catalog:
            return "catalog";
        case peer_role::host:
            return "host";
        }

        throw usage_error("Role is invalid.");
    }

    [[nodiscard]] std::string make_timestamp()
    {
        SYSTEMTIME system_time{};
        GetSystemTime(&system_time);
        char buffer[25]{};
        std::snprintf(
            buffer,
            sizeof(buffer),
            "%04u-%02u-%02uT%02u:%02u:%02u.%03uZ",
            static_cast<unsigned>(system_time.wYear),
            static_cast<unsigned>(system_time.wMonth),
            static_cast<unsigned>(system_time.wDay),
            static_cast<unsigned>(system_time.wHour),
            static_cast<unsigned>(system_time.wMinute),
            static_cast<unsigned>(system_time.wSecond),
            static_cast<unsigned>(system_time.wMilliseconds));
        return std::string(buffer);
    }

    [[nodiscard]] std::string make_uuid()
    {
        UUID uuid{};
        if (UuidCreate(&uuid) != RPC_S_OK)
        {
            throw std::runtime_error("UuidCreate failed.");
        }

        RPC_CSTR text{};
        if (UuidToStringA(&uuid, &text) != RPC_S_OK)
        {
            throw std::runtime_error("UuidToStringA failed.");
        }

        std::string value(reinterpret_cast<char const*>(text));
        RpcStringFreeA(&text);
        return value;
    }

    [[nodiscard]] dearstory::protocol::generated::implementation_identity native_identity()
    {
        return dearstory::protocol::generated::implementation_identity{
            .dearImGuiIdentity = std::string("ocornut/imgui@8936b58fe26e8c3da834b8f60b06511d537b4c63"),
            .dearImGuiVersion = std::string("1.92.8"),
            .language = std::string("cpp"),
            .name = std::string("DearStory.ProtocolProbe.Cpp"),
            .toolchain = std::string("MSVC 14.51"),
            .version = std::string("0.1.0")
        };
    }

    [[nodiscard]] dearstory::protocol::control_envelope build_hello(options const& options)
    {
        return dearstory::protocol::control_envelope{
            .protocol = dearstory::protocol::version{ options.protocol_major, options.protocol_minor },
            .type = "hello",
            .message_id = make_uuid(),
            .timestamp = make_timestamp(),
            .payload = dearstory::protocol::generated::hello{
                .implementation = native_identity(),
                .requiredCapabilities = options.required_capabilities,
                .role = parse_role(options.role),
                .supportedCapabilities = { "control.handshake.v1", "story.run" } }
        };
    }

    [[nodiscard]] dearstory::protocol::handshake_policy build_policy()
    {
        return dearstory::protocol::handshake_policy{
            .local_version = dearstory::protocol::version{ dearstory::protocol::current_major, dearstory::protocol::current_minor },
            .local_implementation = native_identity(),
            .supported_capabilities = { "control.handshake.v1", "story.run" },
            .create_uuid = [] { return make_uuid(); },
            .create_timestamp = [] { return make_timestamp(); }
        };
    }

    void emit_diagnostic(std::string_view category, std::string_view code, std::string_view message)
    {
        auto json = nlohmann::json{
            { "category", category },
            { "code", code },
            { "message", message }
        };
        std::cerr << json.dump() << '\n';
    }

    void emit_summary(dearstory::protocol::control_envelope const& envelope)
    {
        if (auto const* welcome = std::get_if<dearstory::protocol::generated::welcome>(&envelope.payload))
        {
            std::cout << "WELCOME protocol=" << envelope.protocol.major << "." << envelope.protocol.minor;
            std::cout << " accepted=";
            for (std::size_t index = 0; index < welcome->acceptedCapabilities.size(); ++index)
            {
                if (index > 0)
                {
                    std::cout << ',';
                }

                std::cout << welcome->acceptedCapabilities[index];
            }

            std::cout << '\n';
            return;
        }

        if (auto const* reject = std::get_if<dearstory::protocol::generated::reject>(&envelope.payload))
        {
            std::cout << "REJECT code=" << reject->error.code << " recovery=" << reject->error.recovery << '\n';
        }
    }

    [[nodiscard]] options parse_arguments(int argc, char** argv)
    {
        if (argc < 2)
        {
            throw usage_error("Usage: dearstory-protocol-probe-cpp <serve|connect> --pipe <name> [options]");
        }

        options parsed{};
        parsed.mode = argv[1];

        for (int index = 2; index < argc; ++index)
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
                parsed.pipe = require_value("--pipe");
            }
            else if (argument == "--role")
            {
                parsed.role = require_value("--role");
            }
            else if (argument == "--require")
            {
                parsed.required_capabilities.push_back(require_value("--require"));
            }
            else if (argument == "--protocol-major")
            {
                parsed.protocol_major = static_cast<std::uint16_t>(std::stoi(require_value("--protocol-major")));
            }
            else if (argument == "--protocol-minor")
            {
                parsed.protocol_minor = static_cast<std::uint16_t>(std::stoi(require_value("--protocol-minor")));
            }
            else if (argument == "--once")
            {
                continue;
            }
            else
            {
                throw usage_error("Unknown argument: " + argument);
            }
        }

        if (parsed.pipe.empty())
        {
            throw usage_error("The --pipe argument is required.");
        }

        return parsed;
    }

    class timeout_scope final {
    public:
        timeout_scope()
            : timer_([this](std::stop_token token) {
                auto const deadline = std::chrono::steady_clock::now() + std::chrono::seconds(10);
                while (!token.stop_requested() && std::chrono::steady_clock::now() < deadline)
                {
                    std::this_thread::sleep_for(std::chrono::milliseconds(25));
                }

                if (!token.stop_requested())
                {
                    stop_source_.request_stop();
                }
            })
        {
        }

        [[nodiscard]] std::stop_token token() const noexcept
        {
            return stop_source_.get_token();
        }

    private:
        std::stop_source stop_source_{};
        std::jthread timer_;
    };

    [[nodiscard]] int run_server(options const& options)
    {
        timeout_scope timeout{};
        dearstory::protocol::windows::named_pipe_server server(normalize_pipe_path(options.pipe));
        auto connection = server.accept(timeout.token());
        auto payload = connection.read_payload(timeout.token());
        if (!payload.has_value())
        {
            emit_diagnostic("pipe", "protocol.pipe_closed", "The client disconnected before sending a hello envelope.");
            return exit_pipe;
        }

        auto const decoded = dearstory::protocol::decode(*payload);
        if (!decoded.has_value())
        {
            emit_diagnostic("protocol", decoded.error().code, decoded.error().message);
            return exit_protocol;
        }

        auto response = dearstory::protocol::negotiate(*decoded, build_policy());
        connection.write_payload(dearstory::protocol::encode(response));
        emit_summary(response);
        return response.type == "welcome" ? exit_success : exit_protocol;
    }

    [[nodiscard]] int run_client(options const& options)
    {
        timeout_scope timeout{};
        auto connection = dearstory::protocol::windows::named_pipe_client::connect(normalize_pipe_path(options.pipe), timeout.token());
        auto hello = build_hello(options);
        connection.write_payload(dearstory::protocol::encode(hello));
        auto response_payload = connection.read_payload(timeout.token());
        if (!response_payload.has_value())
        {
            emit_diagnostic("pipe", "protocol.pipe_closed", "The server closed the connection before responding.");
            return exit_pipe;
        }

        auto const decoded = dearstory::protocol::decode(*response_payload);
        if (!decoded.has_value())
        {
            emit_diagnostic("protocol", decoded.error().code, decoded.error().message);
            return exit_protocol;
        }

        emit_summary(*decoded);
        return decoded->type == "welcome" ? exit_success : exit_protocol;
    }
} // namespace

int main(int argc, char** argv)
{
    try
    {
        auto const options = parse_arguments(argc, argv);
        if (options.mode == "serve")
        {
            return run_server(options);
        }

        if (options.mode == "connect")
        {
            return run_client(options);
        }

        throw usage_error("The first argument must be either 'serve' or 'connect'.");
    }
    catch (usage_error const& exception)
    {
        std::cerr << exception.what() << '\n';
        return exit_usage;
    }
    catch (dearstory::protocol::windows::pipe_exception const& exception)
    {
        if (exception.error().code == "protocol.operation_cancelled")
        {
            emit_diagnostic("timeout", exception.error().code, exception.error().message);
            return exit_timeout;
        }

        emit_diagnostic("pipe", exception.error().code, exception.error().message);
        return exit_pipe;
    }
    catch (std::exception const& exception)
    {
        emit_diagnostic("protocol", "protocol.unhandled_exception", exception.what());
        return exit_protocol;
    }
}
