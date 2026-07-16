#define NOMINMAX
#include <Windows.h>

#include <dearstory/protocol/windows/named_pipe_client.hpp>

#include <string>

namespace dearstory::protocol::windows {
namespace
{
    [[nodiscard]] protocol_error make_pipe_error(
        std::string code,
        std::string message,
        std::string recovery,
        DWORD win32_error,
        std::string_view operation)
    {
        protocol_error error{
            .code = std::move(code),
            .message = std::move(message),
            .recovery = std::move(recovery)
        };
        error.details = nlohmann::json{
            { "operation", operation },
            { "win32Error", win32_error }
        };
        return error;
    }

    [[noreturn]] void throw_pipe_error(
        std::string code,
        std::string message,
        std::string recovery,
        DWORD win32_error,
        std::string_view operation)
    {
        throw pipe_exception(make_pipe_error(
            std::move(code),
            std::move(message),
            std::move(recovery),
            win32_error,
            operation));
    }
} // namespace

pipe_connection named_pipe_client::connect(std::wstring_view pipe_name, std::stop_token stop_token)
{
    auto const full_pipe_name = std::wstring(pipe_name);
    while (!stop_token.stop_requested())
    {
        auto* handle = CreateFileW(
            full_pipe_name.c_str(),
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            nullptr);

        if (handle != INVALID_HANDLE_VALUE)
        {
            DWORD mode = PIPE_READMODE_BYTE;
            if (!SetNamedPipeHandleState(handle, &mode, nullptr, nullptr))
            {
                auto const error = GetLastError();
                CloseHandle(handle);
                throw_pipe_error(
                    "protocol.pipe_io_failed",
                    "The named-pipe client could not enter byte mode.",
                    "Reconnect and retry the transport operation.",
                    error,
                    "connect");
            }

            return pipe_connection(handle);
        }

        auto const error = GetLastError();
        if (error != ERROR_PIPE_BUSY && error != ERROR_FILE_NOT_FOUND)
        {
            throw_pipe_error(
                "protocol.pipe_io_failed",
                "The named-pipe client could not connect to the server.",
                "Ensure the server is listening and retry the connection.",
                error,
                "connect");
        }

        WaitNamedPipeW(full_pipe_name.c_str(), 50);
    }

    throw_pipe_error(
        "protocol.operation_cancelled",
        "The named-pipe operation was cancelled.",
        "Retry after reconnecting or restarting the operation.",
        ERROR_OPERATION_ABORTED,
        "connect");
}

} // namespace dearstory::protocol::windows
