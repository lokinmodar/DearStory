#pragma once

#include <deque>
#include <optional>
#include <stdexcept>
#include <stop_token>
#include <string>
#include <string_view>

#include <dearstory/protocol/control_envelope.hpp>
#include <dearstory/protocol/framing.hpp>

namespace dearstory::transports::windows {

/// Represents a Win32 pipe failure surfaced as a protocol_error payload.
class pipe_exception final : public std::runtime_error {
public:
    /// Initializes a new instance of the pipe_exception class.
    /// \param error The machine-readable pipe failure payload.
    explicit pipe_exception(protocol::protocol_error error);

    /// Gets the machine-readable pipe failure.
    /// \returns The captured pipe failure payload.
    [[nodiscard]] protocol::protocol_error const& error() const noexcept;

private:
    protocol::protocol_error error_;
};

/// Represents one framed DearStory control connection over a Windows named pipe.
class pipe_connection final {
public:
    /// Initializes an empty pipe connection.
    pipe_connection() noexcept = default;

    /// Transfers ownership from another pipe connection.
    /// \param other The connection whose handle ownership is being transferred.
    pipe_connection(pipe_connection&& other) noexcept;

    /// Transfers ownership from another pipe connection.
    /// \param other The connection whose handle ownership is being transferred.
    /// \returns A reference to this instance after ownership transfer completes.
    pipe_connection& operator=(pipe_connection&& other) noexcept;

    pipe_connection(pipe_connection const&) = delete;
    pipe_connection& operator=(pipe_connection const&) = delete;

    /// Releases the owned pipe handle.
    ~pipe_connection();

    /// Gets a value that indicates whether the connection owns a live pipe handle.
    /// \returns \c true if the connection currently owns a live pipe handle; otherwise, \c false.
    [[nodiscard]] explicit operator bool() const noexcept;

    /// Reads the next framed UTF-8 control payload from the pipe.
    /// \param stop_token The cancellation token observed while waiting for more bytes.
    /// \returns The next payload when one is available, or \c std::nullopt after a clean end-of-stream.
    [[nodiscard]] std::optional<std::string> read_payload(std::stop_token stop_token = {});

    /// Writes one framed UTF-8 control payload to the pipe.
    /// \param payload The UTF-8 JSON control payload to frame and write.
    void write_payload(std::string_view payload);

private:
    friend class named_pipe_server;
    friend class named_pipe_client;

    explicit pipe_connection(void* handle) noexcept;
    void close() noexcept;

    void* handle_{ nullptr };
    protocol::frame_decoder decoder_{};
    std::deque<std::string> buffered_frames_{};
    bool awaiting_more_bytes_{ false };
};

/// Accepts exactly one DearStory control connection over a named pipe instance.
class named_pipe_server final {
public:
    /// Initializes a new instance of the named_pipe_server class.
    /// \param pipe_name The fully qualified Windows named-pipe path to create.
    explicit named_pipe_server(std::wstring pipe_name);

    /// Transfers ownership from another named_pipe_server.
    /// \param other The server whose pipe instance ownership is being transferred.
    named_pipe_server(named_pipe_server&& other) noexcept;

    /// Transfers ownership from another named_pipe_server.
    /// \param other The server whose pipe instance ownership is being transferred.
    /// \returns A reference to this instance after ownership transfer completes.
    named_pipe_server& operator=(named_pipe_server&& other) noexcept;

    named_pipe_server(named_pipe_server const&) = delete;
    named_pipe_server& operator=(named_pipe_server const&) = delete;

    /// Releases the owned server pipe handle.
    ~named_pipe_server();

    /// Accepts one client connection, observing the supplied stop token.
    /// \param stop_token The cancellation token observed while waiting for a client.
    /// \returns The accepted client connection surface.
    [[nodiscard]] pipe_connection accept(std::stop_token stop_token);

private:
    void close() noexcept;

    std::wstring pipe_name_{};
    void* handle_{ nullptr };
};

} // namespace dearstory::transports::windows
