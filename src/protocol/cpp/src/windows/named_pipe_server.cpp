#define NOMINMAX
#include <Windows.h>

#include <dearstory/protocol/windows/named_pipe_server.hpp>

#include <array>
#include <span>
#include <utility>

namespace dearstory::protocol::windows {
namespace
{
    struct scoped_event final {
        scoped_event()
            : handle(CreateEventW(nullptr, TRUE, FALSE, nullptr))
        {
        }

        ~scoped_event()
        {
            if (handle != nullptr)
            {
                CloseHandle(handle);
            }
        }

        HANDLE handle{ nullptr };
    };

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

    void close_handle(void*& handle) noexcept
    {
        if (handle != nullptr)
        {
            CloseHandle(static_cast<HANDLE>(handle));
            handle = nullptr;
        }
    }

    [[nodiscard]] HANDLE release_handle(void*& handle) noexcept
    {
        auto* released = static_cast<HANDLE>(handle);
        handle = nullptr;
        return released;
    }

    template <typename TStart>
    [[nodiscard]] DWORD run_overlapped(
        HANDLE handle,
        std::stop_token stop_token,
        std::string_view operation,
        TStart&& start,
        bool treat_disconnect_as_eof = false)
    {
        scoped_event event;
        if (event.handle == nullptr)
        {
            throw_pipe_error(
                "protocol.pipe_io_failed",
                "Failed to allocate a Win32 event for overlapped I/O.",
                "Retry after releasing system resources.",
                GetLastError(),
                operation);
        }

        OVERLAPPED overlapped{};
        overlapped.hEvent = event.handle;

        auto const started = start(&overlapped);
        if (!started)
        {
            auto const error = GetLastError();
            if (error == ERROR_PIPE_CONNECTED)
            {
                return 0;
            }

            if (error != ERROR_IO_PENDING)
            {
                if (treat_disconnect_as_eof &&
                    (error == ERROR_BROKEN_PIPE || error == ERROR_PIPE_NOT_CONNECTED))
                {
                    return 0;
                }

                throw_pipe_error(
                    "protocol.pipe_io_failed",
                    "The named-pipe I/O operation could not start.",
                    "Reconnect and retry the transport operation.",
                    error,
                    operation);
            }

            std::stop_callback callback(stop_token, [handle, &overlapped] {
                CancelIoEx(handle, &overlapped);
            });

            auto const wait_result = WaitForSingleObject(event.handle, INFINITE);
            if (wait_result != WAIT_OBJECT_0)
            {
                throw_pipe_error(
                    "protocol.pipe_io_failed",
                    "The named-pipe I/O operation did not signal completion.",
                    "Reconnect and retry the transport operation.",
                    GetLastError(),
                    operation);
            }
        }

        DWORD transferred = 0;
        if (!GetOverlappedResult(handle, &overlapped, &transferred, started ? TRUE : FALSE))
        {
            auto const error = GetLastError();
            if (error == ERROR_OPERATION_ABORTED && stop_token.stop_requested())
            {
                throw_pipe_error(
                    "protocol.operation_cancelled",
                    "The named-pipe operation was cancelled.",
                    "Retry after reconnecting or restarting the operation.",
                    error,
                    operation);
            }

            if (treat_disconnect_as_eof &&
                (error == ERROR_BROKEN_PIPE || error == ERROR_PIPE_NOT_CONNECTED))
            {
                return 0;
            }

            throw_pipe_error(
                "protocol.pipe_io_failed",
                "The named-pipe I/O operation failed.",
                "Reconnect and retry the transport operation.",
                error,
                operation);
        }

        return transferred;
    }

    [[nodiscard]] HANDLE require_handle(void* handle, std::string_view operation)
    {
        if (handle == nullptr)
        {
            throw_pipe_error(
                "protocol.pipe_io_failed",
                "The named-pipe handle is not available.",
                "Create a new pipe connection before retrying the operation.",
                ERROR_INVALID_HANDLE,
                operation);
        }

        return static_cast<HANDLE>(handle);
    }
} // namespace

pipe_exception::pipe_exception(protocol_error error)
    : std::runtime_error(error.message)
    , error_(std::move(error))
{
}

protocol_error const& pipe_exception::error() const noexcept
{
    return error_;
}

pipe_connection::pipe_connection(void* handle) noexcept
    : handle_(handle)
{
}

pipe_connection::pipe_connection(pipe_connection&& other) noexcept
    : handle_(std::exchange(other.handle_, nullptr))
    , decoder_(std::move(other.decoder_))
    , buffered_frames_(std::move(other.buffered_frames_))
    , awaiting_more_bytes_(other.awaiting_more_bytes_)
{
    other.awaiting_more_bytes_ = false;
}

pipe_connection& pipe_connection::operator=(pipe_connection&& other) noexcept
{
    if (this != &other)
    {
        close();
        handle_ = std::exchange(other.handle_, nullptr);
        decoder_ = std::move(other.decoder_);
        buffered_frames_ = std::move(other.buffered_frames_);
        awaiting_more_bytes_ = other.awaiting_more_bytes_;
        other.awaiting_more_bytes_ = false;
    }

    return *this;
}

pipe_connection::~pipe_connection()
{
    close();
}

pipe_connection::operator bool() const noexcept
{
    return handle_ != nullptr;
}

std::optional<std::string> pipe_connection::read_payload()
{
    if (!buffered_frames_.empty())
    {
        auto frame_text = std::move(buffered_frames_.front());
        buffered_frames_.pop_front();
        return frame_text;
    }

    auto const handle = require_handle(handle_, "read");
    std::array<std::byte, 4096> buffer{};
    for (;;)
    {
        auto const bytes_read = run_overlapped(
            handle,
            std::stop_token{},
            "read",
            [&](OVERLAPPED* overlapped) {
                return ReadFile(
                    handle,
                    buffer.data(),
                    static_cast<DWORD>(buffer.size()),
                    nullptr,
                    overlapped);
            },
            true);

        if (bytes_read == 0)
        {
            if (awaiting_more_bytes_)
            {
                throw pipe_exception(protocol_error{
                    .code = "protocol.invalid_envelope",
                    .message = "The control frame ended before the payload completed.",
                    .recovery = "Reconnect and resend a complete control frame."
                });
            }

            return std::nullopt;
        }

        auto const result = decoder_.push(std::span(buffer.data(), static_cast<std::size_t>(bytes_read)));
        if (!result.has_value())
        {
            throw pipe_exception(result.error());
        }

        if (result.value().empty())
        {
            awaiting_more_bytes_ = true;
            continue;
        }

        awaiting_more_bytes_ = false;
        for (auto& frame_text : result.value())
        {
            buffered_frames_.push_back(frame_text);
        }

        auto frame_text = std::move(buffered_frames_.front());
        buffered_frames_.pop_front();
        return frame_text;
    }
}

void pipe_connection::write_payload(std::string_view payload)
{
    auto const handle = require_handle(handle_, "write");
    auto const framed = frame(payload);
    std::size_t offset = 0;
    while (offset < framed.size())
    {
        auto const bytes_written = run_overlapped(
            handle,
            std::stop_token{},
            "write",
            [&](OVERLAPPED* overlapped) {
                return WriteFile(
                    handle,
                    framed.data() + offset,
                    static_cast<DWORD>(framed.size() - offset),
                    nullptr,
                    overlapped);
            });

        offset += bytes_written;
    }
}

void pipe_connection::close() noexcept
{
    close_handle(handle_);
}

named_pipe_server::named_pipe_server(std::wstring pipe_name)
    : pipe_name_(std::move(pipe_name))
{
    auto* handle = CreateNamedPipeW(
        pipe_name_.c_str(),
        PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1,
        max_control_frame_bytes + sizeof(std::uint32_t),
        max_control_frame_bytes + sizeof(std::uint32_t),
        0,
        nullptr);
    if (handle == INVALID_HANDLE_VALUE)
    {
        throw_pipe_error(
            "protocol.pipe_io_failed",
            "The named-pipe server could not be created.",
            "Choose a different pipe name or retry after releasing the existing server instance.",
            GetLastError(),
            "create_server");
    }

    handle_ = handle;
}

named_pipe_server::named_pipe_server(named_pipe_server&& other) noexcept
    : pipe_name_(std::move(other.pipe_name_))
    , handle_(std::exchange(other.handle_, nullptr))
{
}

named_pipe_server& named_pipe_server::operator=(named_pipe_server&& other) noexcept
{
    if (this != &other)
    {
        close();
        pipe_name_ = std::move(other.pipe_name_);
        handle_ = std::exchange(other.handle_, nullptr);
    }

    return *this;
}

named_pipe_server::~named_pipe_server()
{
    close();
}

pipe_connection named_pipe_server::accept(std::stop_token stop_token)
{
    auto const handle = require_handle(handle_, "accept");
    if (stop_token.stop_requested())
    {
        throw_pipe_error(
            "protocol.operation_cancelled",
            "The named-pipe operation was cancelled.",
            "Retry after reconnecting or restarting the operation.",
            ERROR_OPERATION_ABORTED,
            "accept");
    }

    auto const connected = ConnectNamedPipe(handle, nullptr);
    if (connected || GetLastError() == ERROR_PIPE_CONNECTED)
    {
        return pipe_connection(release_handle(handle_));
    }

    auto const error = GetLastError();
    if (error != ERROR_IO_PENDING)
    {
        (void)run_overlapped(
            handle,
            stop_token,
            "accept",
            [&](OVERLAPPED* overlapped) {
                return ConnectNamedPipe(handle, overlapped);
            });
    }
    else
    {
        (void)run_overlapped(
            handle,
            stop_token,
            "accept",
            [&](OVERLAPPED* overlapped) -> BOOL {
                SetLastError(ERROR_IO_PENDING);
                return ConnectNamedPipe(handle, overlapped);
            });
    }

    return pipe_connection(release_handle(handle_));
}

void named_pipe_server::close() noexcept
{
    close_handle(handle_);
}

} // namespace dearstory::protocol::windows
