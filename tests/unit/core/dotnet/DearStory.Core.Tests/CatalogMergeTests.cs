using Xunit;

namespace DearStory.Core.Tests;

public sealed class CatalogMergeTests
{
    [Fact]
    public void Merge_rejects_duplicate_canonical_ids_from_different_hosts()
    {
        var catalog = new StoryCatalog();
        catalog.Merge("cpp-host", [StoryDescriptor.Create("buttons/primary", "Buttons/Primary")]);

        var result = catalog.Merge("dotnet-host", [StoryDescriptor.Create("Buttons/Primary", "Buttons/Primary")]);

        Assert.False(result.Succeeded);
        Assert.Single(result.Diagnostics);
        Assert.Equal("story.duplicate_id", result.Diagnostics[0].Code);
    }

    [Fact]
    public void Merge_returns_sorted_catalog_for_unique_ids()
    {
        var catalog = new StoryCatalog();

        var result = catalog.Merge(
            "dotnet-host",
            [
                StoryDescriptor.Create("buttons/secondary", "Buttons/Secondary"),
                StoryDescriptor.Create("buttons/primary", "Buttons/Primary")
            ]);

        Assert.True(result.Succeeded);
        Assert.Equal(["buttons/primary", "buttons/secondary"], result.Stories.Select(static item => item.Id.Value));
    }
}
