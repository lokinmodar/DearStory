#include <dearstory/core/story_catalog.hpp>

#include <stdexcept>

namespace dearstory::core {

story_descriptor story_descriptor::create(std::string_view raw_id, std::string_view story_title)
{
    auto identifier = story_id::parse(raw_id);
    if (!identifier.has_value())
    {
        throw std::invalid_argument("The story identifier must contain at least one non-empty segment.");
    }

    return story_descriptor{
        .id = std::move(identifier).value(),
        .title = std::string(story_title),
    };
}

catalog_merge_result story_catalog::merge(std::string_view host_id, std::vector<story_descriptor> stories)
{
    if (host_id.empty())
    {
        throw std::invalid_argument("The host identifier must not be empty.");
    }

    std::vector<catalog_diagnostic> diagnostics;
    for (auto& story : stories)
    {
        auto const& canonical_id = story.id.value();
        auto const existing_host = story_hosts_.find(canonical_id);
        if (existing_host != story_hosts_.end() && existing_host->second != host_id)
        {
            diagnostics.push_back(catalog_diagnostic{
                .code = "story.duplicate_id",
                .message = "The story '" + canonical_id + "' is already published by host '" + existing_host->second + "'.",
                .host_id = std::string(host_id),
                .story = story.id,
            });
            continue;
        }

        story_hosts_.insert_or_assign(canonical_id, std::string(host_id));
        stories_.insert_or_assign(canonical_id, story);
    }

    return catalog_merge_result{
        .succeeded = diagnostics.empty(),
        .stories = merged_stories(),
        .diagnostics = std::move(diagnostics),
    };
}

std::vector<story_descriptor> story_catalog::merged_stories() const
{
    std::vector<story_descriptor> stories;
    stories.reserve(stories_.size());

    for (auto const& [canonical_id, descriptor] : stories_)
    {
        static_cast<void>(canonical_id);
        stories.push_back(descriptor);
    }

    return stories;
}

} // namespace dearstory::core
