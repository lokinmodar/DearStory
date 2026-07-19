using DearStory.Sdk;

public sealed class PrimaryButtonArgs
{
    [StoryArg("label")]
    public string Label { get; init; } = "Save";
}

public static class ButtonStories
{
    [Story("buttons/package-smoke", typeof(PrimaryButtonArgs))]
    public static void PrimaryButton(StoryContext context)
    {
        _ = context.Args;
    }
}
