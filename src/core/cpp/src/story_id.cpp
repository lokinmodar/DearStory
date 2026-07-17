#include <dearstory/core/story_id.hpp>

#include <algorithm>
#include <cctype>
#include <vector>

namespace dearstory::core {
namespace
{
    [[nodiscard]] std::string_view trim_ascii_whitespace(std::string_view value) noexcept
    {
        while (!value.empty() && std::isspace(static_cast<unsigned char>(value.front())) != 0)
        {
            value.remove_prefix(1);
        }

        while (!value.empty() && std::isspace(static_cast<unsigned char>(value.back())) != 0)
        {
            value.remove_suffix(1);
        }

        return value;
    }

    [[nodiscard]] std::string lowercase_normalized(std::string_view raw)
    {
        std::string result;
        result.reserve(raw.size());

        for (char character : raw)
        {
            auto const normalized = character == '\\' ? '/' : character;
            result.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(normalized))));
        }

        return result;
    }
} // namespace

story_id::story_id(std::string canonical_value)
    : value_(std::move(canonical_value))
{
}

std::optional<story_id> story_id::parse(std::string_view raw)
{
    auto const trimmed = trim_ascii_whitespace(raw);
    if (trimmed.empty())
    {
        return std::nullopt;
    }

    auto normalized = lowercase_normalized(trimmed);
    std::vector<std::string> segments;
    std::size_t start = 0;

    while (start <= normalized.size())
    {
        auto const separator = normalized.find('/', start);
        auto const count = separator == std::string::npos ? normalized.size() - start : separator - start;
        auto const segment = trim_ascii_whitespace(std::string_view{ normalized }.substr(start, count));
        if (!segment.empty())
        {
            segments.emplace_back(segment);
        }

        if (separator == std::string::npos)
        {
            break;
        }

        start = separator + 1;
    }

    if (segments.empty())
    {
        return std::nullopt;
    }

    std::string canonical = segments.front();
    for (std::size_t index = 1; index < segments.size(); ++index)
    {
        canonical.push_back('/');
        canonical.append(segments[index]);
    }

    return story_id{ std::move(canonical) };
}

std::string const& story_id::value() const noexcept
{
    return value_;
}

bool operator==(story_id const& identifier, std::string_view raw) noexcept
{
    return identifier.value() == raw;
}

bool operator==(std::string_view raw, story_id const& identifier) noexcept
{
    return identifier == raw;
}

} // namespace dearstory::core
