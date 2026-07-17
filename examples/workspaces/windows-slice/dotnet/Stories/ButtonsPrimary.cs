using DearStory.Sdk;
using ImGuiNET;

namespace DearStory.Examples.WindowsSlice;

/// <summary>
/// Declares the first managed DearStory story baseline used by the Windows slice.
/// </summary>
public static class ButtonsPrimary
{
    /// <summary>
    /// Renders the managed primary-button story through ImGui.NET.
    /// </summary>
    /// <param name="context">The active DearStory story context.</param>
    [Story("Buttons/PrimaryManaged", typeof(ButtonsPrimaryArgs))]
    public static void Render(StoryContext context)
    {
        var label = context.Args["label"]?.GetValue<string>() ?? "Save";
        _ = ImGui.Button(label);
    }
}

/// <summary>
/// Describes the serializable arguments for the managed primary-button story.
/// </summary>
public sealed class ButtonsPrimaryArgs
{
    /// <summary>
    /// Gets the label rendered on the managed primary button.
    /// </summary>
    /// <value>The button caption. The default is <c>Save</c>.</value>
    [StoryArg("label")]
    public string Label { get; init; } = "Save";
}
