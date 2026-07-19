#define NOMINMAX
#include <Windows.h>
#include <Rpc.h>

#include <dearstory/hosts/cpp/native_host.hpp>

#include <imgui.h>
#include <nlohmann/json.hpp>

#include <dearstory/core/story_id.hpp>
#include <dearstory/core/story_session.hpp>
#include <dearstory/protocol/codec.hpp>
#include <dearstory/protocol/generated/messages.hpp>
#include <dearstory/protocol/version.hpp>
#include <dearstory/transports/windows/named_pipe_client.hpp>
#include <dearstory/sdk/story_context.hpp>
#include <dearstory/sdk/story_registry.hpp>
#include <dearstory/transports/windows/shared_memory_frame_channel.hpp>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <optional>
#include <span>
#include <stdexcept>
#include <string>
#include <string_view>
#include <thread>
#include <utility>
#include <vector>

namespace dearstory::examples::windows_slice
{
    void register_windows_slice_cpp_stories(sdk::story_registry& registry);
}

namespace dearstory::hosts::cpp {
namespace
{
    constexpr int exit_success = 0;
    constexpr int exit_pipe = 21;
    constexpr int exit_protocol = 22;
    constexpr int default_width = 320;
    constexpr int default_height = 180;
    constexpr int default_stride = default_width * 4;
    constexpr int default_slot_count = 3;
    constexpr std::string_view imgui_identity = "ocornut/imgui@8936b58fe26e8c3da834b8f60b06511d537b4c63";
    constexpr std::string_view imgui_version = "1.92.8";
    constexpr std::string_view schema_dialect = "https://json-schema.org/draft/2020-12/schema";

    struct timeout_scope final {
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

    struct session_request final {
        std::string message_id{};
        std::string session_id{};
        std::string story_id{};
        std::string random_seed{};
        std::string start_time_utc{};
        nlohmann::json initial_arguments = nlohmann::json::object();
    };

    class imgui_context_scope final {
    public:
        imgui_context_scope()
        {
            IMGUI_CHECKVERSION();
            context_ = ImGui::CreateContext();
        }

        imgui_context_scope(imgui_context_scope const&) = delete;
        imgui_context_scope& operator=(imgui_context_scope const&) = delete;

        ~imgui_context_scope()
        {
            if (context_ != nullptr)
            {
                ImGui::DestroyContext(context_);
            }
        }

    private:
        ImGuiContext* context_{ nullptr };
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
            throw std::runtime_error("The supplied UTF-8 text could not be converted to UTF-16.");
        }

        std::wstring wide(static_cast<std::size_t>(size), L'\0');
        (void)MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), wide.data(), size);
        return wide;
    }

    [[nodiscard]] std::wstring normalize_pipe_path(std::string_view pipe_name)
    {
        auto wide = to_wide(pipe_name);
        static constexpr std::wstring_view prefix = LR"(\\.\pipe\)";
        if (wide.rfind(prefix, 0) == 0)
        {
            return wide;
        }

        return std::wstring(prefix) + wide;
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

    [[nodiscard]] std::chrono::sys_time<std::chrono::milliseconds> current_utc_time()
    {
        return std::chrono::time_point_cast<std::chrono::milliseconds>(std::chrono::system_clock::now());
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

    [[nodiscard]] protocol::generated::implementation_identity native_identity()
    {
        return protocol::generated::implementation_identity{
            .dearImGuiIdentity = std::string(imgui_identity),
            .dearImGuiVersion = std::string(imgui_version),
            .language = "cpp",
            .name = "DearStory.Host.Cpp",
            .toolchain = std::string("MSVC ") + std::to_string(_MSC_FULL_VER),
            .version = "0.1.0",
        };
    }

    [[nodiscard]] protocol::control_envelope build_hello()
    {
        return protocol::control_envelope{
            .protocol = protocol::version{ protocol::current_major, protocol::current_minor },
            .type = "hello",
            .message_id = make_uuid(),
            .timestamp = make_timestamp(),
            .payload = protocol::generated::hello{
                .implementation = native_identity(),
                .requiredCapabilities = {},
                .role = protocol::generated::peer_role::host,
                .supportedCapabilities = { "control.handshake.v1", "story.run" },
            },
        };
    }

    [[nodiscard]] std::string require_string(nlohmann::json const& object, char const* name)
    {
        auto const iterator = object.find(name);
        if (iterator == object.end() || !iterator->is_string())
        {
            throw std::runtime_error(std::string("Missing or invalid string field '") + name + "'.");
        }

        return iterator->get<std::string>();
    }

    [[nodiscard]] nlohmann::json make_protocol_json(protocol::version value)
    {
        return nlohmann::json{
            { "major", value.major },
            { "minor", value.minor },
        };
    }

    void write_json_envelope(
        transports::windows::pipe_connection& connection,
        protocol::version version,
        std::string_view type,
        nlohmann::json payload,
        std::optional<std::string> correlation_id = std::nullopt,
        std::optional<std::string> session_id = std::nullopt)
    {
        auto envelope = nlohmann::json{
            { "protocol", make_protocol_json(version) },
            { "type", type },
            { "messageId", make_uuid() },
            { "timestamp", make_timestamp() },
            { "payload", std::move(payload) },
        };

        if (correlation_id.has_value())
        {
            envelope["correlationId"] = *correlation_id;
        }

        if (session_id.has_value())
        {
            envelope["sessionId"] = *session_id;
        }

        connection.write_payload(envelope.dump());
    }

    [[nodiscard]] sdk::story_registration const* find_registration(
        sdk::story_registry const& registry,
        std::string_view story_id)
    {
        auto const canonical = core::story_id::parse(story_id);
        if (!canonical.has_value())
        {
            return nullptr;
        }

        for (auto const& registration : registry.registrations())
        {
            if (registration.descriptor().id == canonical->value())
            {
                return &registration;
            }
        }

        return nullptr;
    }

    [[nodiscard]] session_request parse_session_request(nlohmann::json const& envelope)
    {
        auto const payload_iterator = envelope.find("payload");
        if (payload_iterator == envelope.end() || !payload_iterator->is_object())
        {
            throw std::runtime_error("The story session open envelope payload is missing.");
        }

        auto const& payload = *payload_iterator;
        session_request request{
            .message_id = require_string(envelope, "messageId"),
            .session_id = require_string(payload, "sessionId"),
            .story_id = require_string(payload, "storyId"),
            .random_seed = require_string(payload, "randomSeed"),
            .start_time_utc = require_string(payload, "startTimeUtc"),
            .initial_arguments = payload.contains("initialArguments")
                ? payload.at("initialArguments")
                : nlohmann::json::object(),
        };

        if (!request.initial_arguments.is_object())
        {
            throw std::runtime_error("The story session initialArguments payload must be an object.");
        }

        return request;
    }

    [[nodiscard]] nlohmann::json serialize_story_descriptor(sdk::story_registration const& registration)
    {
        auto const& descriptor = registration.descriptor();
        auto const& arguments = registration.arguments();

        auto json = nlohmann::json{
            { "argumentSchema",
                {
                    { "dialect", schema_dialect },
                    { "schema", arguments.schema().document() },
                } },
            { "capabilities", descriptor.capabilities },
            { "defaultArguments", arguments.default_arguments() },
            { "hierarchy", descriptor.hierarchy },
            { "id", descriptor.id.value() },
            { "tags", descriptor.tags },
            { "title", descriptor.title },
        };

        if (descriptor.description.has_value())
        {
            json["description"] = *descriptor.description;
        }

        if (descriptor.source_path.has_value())
        {
            json["sourcePath"] = *descriptor.source_path;
        }

        return json;
    }

    [[nodiscard]] nlohmann::json build_story_index_payload(std::string_view host_id, sdk::story_registry const& registry)
    {
        nlohmann::json stories = nlohmann::json::array();
        for (auto const& registration : registry.registrations())
        {
            stories.push_back(serialize_story_descriptor(registration));
        }

        return nlohmann::json{
            { "hostId", host_id },
            { "stories", std::move(stories) },
        };
    }

    void set_pixel(std::vector<std::byte>& pixels, int stride, int x, int y, unsigned char red, unsigned char green, unsigned char blue, unsigned char alpha)
    {
        auto const offset = static_cast<std::size_t>((y * stride) + (x * 4));
        pixels[offset + 0] = static_cast<std::byte>(red);
        pixels[offset + 1] = static_cast<std::byte>(green);
        pixels[offset + 2] = static_cast<std::byte>(blue);
        pixels[offset + 3] = static_cast<std::byte>(alpha);
    }

    [[nodiscard]] std::vector<std::byte> rasterize_draw_bounds(ImDrawData const* draw_data)
    {
        std::vector<std::byte> pixels(static_cast<std::size_t>(default_height * default_stride), std::byte{ 0 });

        for (int y = 0; y < default_height; ++y)
        {
            for (int x = 0; x < default_width; ++x)
            {
                set_pixel(pixels, default_stride, x, y, 24, 26, 31, 255);
            }
        }

        float min_x = static_cast<float>(default_width);
        float min_y = static_cast<float>(default_height);
        float max_x = 0.0F;
        float max_y = 0.0F;
        bool has_vertices = false;

        if (draw_data != nullptr)
        {
            for (int list_index = 0; list_index < draw_data->CmdListsCount; ++list_index)
            {
                auto const* command_list = draw_data->CmdLists[list_index];
                for (auto const& vertex : command_list->VtxBuffer)
                {
                    min_x = std::min(min_x, vertex.pos.x);
                    min_y = std::min(min_y, vertex.pos.y);
                    max_x = std::max(max_x, vertex.pos.x);
                    max_y = std::max(max_y, vertex.pos.y);
                    has_vertices = true;
                }
            }
        }

        int left = 64;
        int top = 48;
        int right = default_width - 64;
        int bottom = default_height - 48;

        if (has_vertices)
        {
            left = std::clamp(static_cast<int>(std::floor(min_x)), 0, default_width - 1);
            top = std::clamp(static_cast<int>(std::floor(min_y)), 0, default_height - 1);
            right = std::clamp(static_cast<int>(std::ceil(max_x)), left + 1, default_width);
            bottom = std::clamp(static_cast<int>(std::ceil(max_y)), top + 1, default_height);
        }

        for (int y = top; y < bottom; ++y)
        {
            for (int x = left; x < right; ++x)
            {
                auto const on_border = x == left || x == right - 1 || y == top || y == bottom - 1;
                if (on_border)
                {
                    set_pixel(pixels, default_stride, x, y, 217, 226, 236, 255);
                }
                else
                {
                    set_pixel(pixels, default_stride, x, y, 72, 136, 255, 255);
                }
            }
        }

        return pixels;
    }

    // The first Windows host slice publishes a deterministic RGBA preview without standing up a
    // full Dear ImGui platform/renderer backend. The official Dear ImGui dependency is still
    // pinned and compiled for story compatibility, while the baseline frame transport and host
    // protocol flow are validated independently from GPU-backed rasterization.
    [[nodiscard]] std::vector<std::byte> render_story_frame(sdk::story_registration const&, core::story_session&)
    {
        return rasterize_draw_bounds(nullptr);
    }

    void publish_session_started(
        transports::windows::pipe_connection& connection,
        protocol::version version,
        session_request const& request)
    {
        write_json_envelope(
            connection,
            version,
            "story_session_opened",
            nlohmann::json{
                { "activeArguments", request.initial_arguments },
                { "randomSeed", request.random_seed },
                { "sessionId", request.session_id },
                { "startTimeUtc", request.start_time_utc },
                { "storyId", request.story_id },
            },
            request.message_id,
            request.session_id);
    }

    void publish_frame_channel_ready(
        transports::windows::pipe_connection& connection,
        protocol::version version,
        session_request const& request,
        std::string_view mapping_name)
    {
        write_json_envelope(
            connection,
            version,
            "frame_channel_ready",
            nlohmann::json{
                { "colorSpace", "srgb" },
                { "height", default_height },
                { "mappingName", mapping_name },
                { "pixelFormat", "rgba8" },
                { "sessionId", request.session_id },
                { "slotCount", default_slot_count },
                { "stride", default_stride },
                { "width", default_width },
            },
            std::nullopt,
            request.session_id);
    }

    void publish_frame_presented(
        transports::windows::pipe_connection& connection,
        protocol::version version,
        session_request const& request,
        transports::windows::published_frame const& frame)
    {
        write_json_envelope(
            connection,
            version,
            "frame_presented",
            nlohmann::json{
                { "sequence", frame.sequence },
                { "sessionId", request.session_id },
                { "slotIndex", frame.slot_index },
                { "timestampUtc", make_timestamp() },
            },
            std::nullopt,
            request.session_id);
    }
} // namespace

native_host::native_host(native_host_options options)
    : options_(std::move(options))
{
}

int native_host::run()
{
    try
    {
        if (options_.pipe_name.empty())
        {
            throw std::runtime_error("The native host requires a pipe name.");
        }

        if (options_.host_id.empty())
        {
            throw std::runtime_error("The native host requires a host identifier.");
        }

        sdk::story_registry registry;
        examples::windows_slice::register_windows_slice_cpp_stories(registry);
        imgui_context_scope imgui_scope{};

        timeout_scope timeout{};
        auto connection = transports::windows::named_pipe_client::connect(normalize_pipe_path(options_.pipe_name), timeout.token());
        connection.write_payload(protocol::encode(build_hello()));

        auto const response_payload = connection.read_payload(timeout.token());
        if (!response_payload.has_value())
        {
            throw std::runtime_error("The runner disconnected before completing the DearStory handshake.");
        }

        auto const response = protocol::decode(*response_payload);
        if (!response.has_value())
        {
            throw std::runtime_error(response.error().message);
        }

        if (response->type == "reject")
        {
            auto const* reject = std::get_if<protocol::generated::reject>(&response->payload);
            throw std::runtime_error(reject == nullptr ? "The runner rejected the native host handshake." : reject->error.message);
        }

        if (response->type != "welcome")
        {
            throw std::runtime_error("The runner responded with an unexpected control message.");
        }

        write_json_envelope(
            connection,
            response->protocol,
            "story_index_published",
            build_story_index_payload(options_.host_id, registry));

        transports::windows::shared_memory_frame_channel* active_channel = nullptr;
        std::optional<transports::windows::shared_memory_frame_channel> owned_channel;

        while (true)
        {
            auto const payload = connection.read_payload();
            if (!payload.has_value())
            {
                return exit_success;
            }

            auto const envelope = nlohmann::json::parse(*payload);
            auto const type = require_string(envelope, "type");
            if (type != "story_session_open")
            {
                continue;
            }

            auto const request = parse_session_request(envelope);
            auto const* registration = find_registration(registry, request.story_id);
            if (registration == nullptr)
            {
                throw std::runtime_error("The requested story is not registered by the native host.");
            }

            auto const story_identifier = core::story_id::parse(request.story_id);
            if (!story_identifier.has_value())
            {
                throw std::runtime_error("The requested story identifier is invalid.");
            }

            auto const seed = static_cast<std::uint64_t>(std::stoull(request.random_seed));
            auto session = core::story_session::open(
                request.session_id,
                *story_identifier,
                request.initial_arguments,
                seed,
                current_utc_time());

            publish_session_started(connection, response->protocol, request);

            auto const mapping_name = "Local\\dearstory-frame-" + request.session_id;
            auto descriptor = transports::windows::frame_transport_descriptor::create(
                to_wide(mapping_name),
                default_width,
                default_height,
                default_stride,
                default_slot_count);

            owned_channel.emplace(std::move(descriptor));
            active_channel = &*owned_channel;

            publish_frame_channel_ready(connection, response->protocol, request, mapping_name);
            auto const pixels = render_story_frame(*registration, session);
            auto const published = active_channel->publish(std::span<const std::byte>(pixels.data(), pixels.size()));
            publish_frame_presented(connection, response->protocol, request, published);
        }
    }
    catch (transports::windows::pipe_exception const&)
    {
        return exit_pipe;
    }
    catch (std::exception const&)
    {
        return exit_protocol;
    }
}

} // namespace dearstory::hosts::cpp
