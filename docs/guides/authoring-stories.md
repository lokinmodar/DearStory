# Authoring DearStory stories

## Purpose

This guide defines how story authors publish DearStory stories in the current implementation slice for native C++ and .NET.

The rules here are intentionally conservative:

- use Dear ImGui or ImGui.NET directly;
- publish metadata through the DearStory SDK only;
- keep every emitted artifact serializable and deterministic.

## Authoring rules that apply in both languages

### Story IDs

Use slash-delimited IDs such as `buttons/primary`.

The core libraries canonicalize IDs by:

- trimming outer whitespace;
- normalizing `\` to `/`;
- removing empty segments;
- lowercasing the final ID.

Use already-canonical IDs in source to avoid surprises in review and diagnostics.

### No DearStory-owned widget API

DearStory does not wrap buttons, inputs, layouts, or rendering commands. Story code talks to the underlying binding directly:

- C++: `ImGui::...`
- .NET: `ImGuiNET.ImGui...`

The SDK exists only to expose:

- story registration;
- argument schema/defaults;
- session state;
- actions, logs, and interaction targets.

### Arguments

Arguments must be serializable and described by the approved schema subset. Keep the surface simple:

- string
- boolean
- integer
- number
- enum values serialized as strings

If the host needs richer editor behavior later, add that through schema-compatible metadata rather than custom runtime objects.

### Actions, logs, and targets

Use the story context to emit serializable artifacts:

- `actions` for intentional semantic events such as “clicked” or “submitted”;
- `logs` for diagnostic output visible to tooling;
- `targets` for semantic interaction anchors that automation can identify later.

Target semantic metadata is the stable cross-language surface for accessibility-oriented meaning:

- `role`
- `accessible_name`
- `description`

Bounds are intentionally JSON-shaped for now because each host may compute them differently.

## C++ authoring

### 1. Define an argument type

```cpp
struct primary_button_args final {
    std::string label{ "Save" };
    bool disabled{ false };
};
```

### 2. Specialize `describe_arguments<T>()`

```cpp
template <>
inline dearstory::sdk::argument_metadata dearstory::sdk::describe_arguments<primary_button_args>()
{
    auto schema = dearstory::core::argument_schema::parse(R"json(
        {
          "type": "object",
          "properties": {
            "label": { "type": "string", "default": "Save" },
            "disabled": { "type": "boolean", "default": false }
          },
          "required": ["label", "disabled"]
        }
    )json").value();

    nlohmann::json defaults = {
        { "label", "Save" },
        { "disabled", false }
    };

    std::vector<dearstory::sdk::argument_descriptor> descriptors = {
        {
            .name = "label",
            .schema = nlohmann::json::parse(R"({"type":"string","default":"Save"})"),
            .default_value = "Save",
            .description = "Caption shown on the button"
        },
        {
            .name = "disabled",
            .schema = nlohmann::json::parse(R"({"type":"boolean","default":false})"),
            .default_value = false,
            .description = "Whether the button is disabled"
        }
    };

    return dearstory::sdk::argument_metadata(
        std::move(schema),
        std::move(defaults),
        std::move(descriptors));
}
```

### 3. Render through Dear ImGui and emit metadata

```cpp
void render_primary_button(dearstory::sdk::story_context& context)
{
    auto const label = context.args().at("label").get<std::string>();
    auto const disabled = context.args().at("disabled").get<bool>();

    if (disabled) {
        ImGui::BeginDisabled();
    }

    if (ImGui::Button(label.c_str())) {
        context.actions().push_back({
            .name = "button.clicked",
            .payload = nlohmann::json{ { "label", label } },
            .emitted_at_utc = context.clock().current_utc(),
            .target_id = "primary-button"
        });
    }

    if (disabled) {
        ImGui::EndDisabled();
    }

    context.targets().push_back({
        .id = "primary-button",
        .bounds = std::nullopt,
        .semantic = dearstory::core::interaction_target_semantic_metadata{
            .role = "button",
            .accessible_name = "Primary action",
            .description = "Commits the current form"
        }
    });
}
```

### 4. Register the story

```cpp
auto registration =
    DEARSTORY_STORY("buttons/primary", render_primary_button, primary_button_args);
```

Add the registration to a `dearstory::sdk::story_registry`. Duplicate canonical IDs are rejected when added.

## .NET authoring

### 1. Define an argument type with `StoryArg`

```csharp
using DearStory.Sdk;

public sealed class PrimaryButtonArgs
{
    /// <summary>Caption shown on the button.</summary>
    [StoryArg("label")]
    public string Label { get; init; } = "Save";

    /// <summary>Whether the button is disabled.</summary>
    [StoryArg("disabled")]
    public bool Disabled { get; init; }
}
```

### 2. Declare a static story method with `Story`

```csharp
using System.Text.Json.Nodes;
using DearStory.Core.Events;
using DearStory.Core.Targets;
using DearStory.Sdk;
using ImGuiNET;

public static class ButtonStories
{
    /// <summary>Primary button story.</summary>
    [Story("buttons/primary", typeof(PrimaryButtonArgs))]
    public static void PrimaryButton(StoryContext context)
    {
        var args = context.Args.AsObject();
        var label = args["label"]!.GetValue<string>();
        var disabled = args["disabled"]!.GetValue<bool>();

        if (disabled)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(label))
        {
            context.Actions.Add(new ActionEvent
            {
                Name = "button.clicked",
                Payload = new JsonObject { ["label"] = label },
                EmittedAtUtc = context.Clock.CurrentUtc,
                TargetId = "primary-button",
            });
        }

        if (disabled)
        {
            ImGui.EndDisabled();
        }

        context.Targets.Add(new InteractionTarget
        {
            Id = "primary-button",
            Semantic = new InteractionTargetSemanticMetadata
            {
                Role = "button",
                AccessibleName = "Primary action",
                Description = "Commits the current form",
            },
        });
    }
}
```

### 3. Prefer the generated registry

```csharp
GeneratedStoryRegistry registry = GeneratedStoryRegistry.Create();
```

The source generator reads:

- `[Story]` method declarations;
- `[StoryArg]` argument properties;
- XML summary comments on the story method and properties;
- property initializers for default values.

That becomes the canonical managed registry payload.

### 4. Use reflection only by explicit opt-in

```csharp
GeneratedStoryRegistry registry = ReflectionStoryRegistry.Create(
    typeof(ButtonStories).Assembly,
    new ReflectionStoryRegistryOptions
    {
        AllowReflectionFallback = true,
    });
```

Use this only when source generation cannot be used. Reflection fallback is intentionally gated so it never becomes the accidental default.

## Documentation today

The current slice uses:

- Markdown under `docs/` for architecture and contributor guidance;
- XML comments for public .NET API surface and story descriptions;
- Doxygen comments for public C++ API surface.

Markdown Doc Blocks are part of the approved project direction, but executable Markdown is not implemented in this slice. Today, the machine-readable source of truth for story controls is the argument schema plus the story metadata generated from source.

## Review checklist for story authors

- ID is already canonical and stable.
- Story code calls the host binding directly, not a DearStory widget wrapper.
- All args have schema/default metadata.
- Actions, logs, and targets are serializable.
- Semantic target metadata is populated where it helps automation/accessibility.
- Public story-facing helper APIs are documented in code comments.
