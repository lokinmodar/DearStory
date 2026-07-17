#include <dearstory/sdk/story_registry.hpp>

#include <algorithm>
#include <cctype>
#include <stdexcept>
#include <string>
#include <vector>

namespace dearstory::sdk {
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

    [[nodiscard]] std::vector<std::string> split_story_segments(std::string_view raw_id)
    {
        auto const trimmed = trim_ascii_whitespace(raw_id);
        std::string normalized;
        normalized.reserve(trimmed.size());

        for (char character : trimmed)
        {
            normalized.push_back(character == '\\' ? '/' : character);
        }

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

        return segments;
    }
} // namespace

story_registration::story_registration(
    core::story_descriptor descriptor,
    argument_metadata arguments,
    story_callback render) noexcept
    : descriptor_(std::move(descriptor))
    , arguments_(std::move(arguments))
    , render_(render)
{
}

story_registration story_registration::create(
    std::string_view raw_id,
    story_callback render,
    argument_metadata arguments,
    visual_story_options visual)
{
    if (render == nullptr)
    {
        throw std::invalid_argument("The DearStory story callback must not be null.");
    }

    auto const segments = split_story_segments(raw_id);
    if (segments.empty())
    {
        throw std::invalid_argument("The DearStory story identifier must contain at least one non-empty segment.");
    }

    auto descriptor = core::story_descriptor::create(raw_id, segments.back());
    descriptor.hierarchy.assign(segments.begin(), segments.end() - 1);
    descriptor.visual.supports_capture = true;
    descriptor.visual.include_in_canonical_corpus = visual.include_in_canonical_corpus;

    return story_registration(std::move(descriptor), std::move(arguments), render);
}

core::story_descriptor const& story_registration::descriptor() const noexcept
{
    return descriptor_;
}

argument_metadata const& story_registration::arguments() const noexcept
{
    return arguments_;
}

story_registration::story_callback story_registration::callback() const noexcept
{
    return render_;
}

void story_registry::add(story_registration registration)
{
    auto const duplicate = std::find_if(
        registrations_.begin(),
        registrations_.end(),
        [&registration](story_registration const& existing) {
            return existing.descriptor().id == registration.descriptor().id;
        });

    if (duplicate != registrations_.end())
    {
        throw std::invalid_argument("A DearStory story with the same canonical identifier is already registered.");
    }

    registrations_.push_back(std::move(registration));
}

std::vector<core::story_descriptor> story_registry::descriptors() const
{
    std::vector<core::story_descriptor> descriptors;
    descriptors.reserve(registrations_.size());

    for (auto const& registration : registrations_)
    {
        descriptors.push_back(registration.descriptor());
    }

    std::sort(
        descriptors.begin(),
        descriptors.end(),
        [](core::story_descriptor const& left, core::story_descriptor const& right) {
            return left.id.value() < right.id.value();
        });

    return descriptors;
}

std::vector<story_registration> const& story_registry::registrations() const noexcept
{
    return registrations_;
}

} // namespace dearstory::sdk
