using DearStory.Catalog;
using DearStory.Core;
using Xunit;

namespace DearStory.Catalog.Tests;

public sealed class CatalogTreeBuilderTests
{
    [Fact]
    public void Build_groups_cpp_and_dotnet_stories_under_one_searchable_tree()
    {
        var stories = new[]
        {
            StoryDescriptor.Create("buttons/primary", "Buttons/Primary"),
            StoryDescriptor.Create("buttons/primarymanaged", "Buttons/PrimaryManaged"),
        };

        var tree = CatalogTreeBuilder.Build(stories);

        Assert.Equal("Buttons", Assert.Single(tree.Children).Title);
        Assert.Equal(2, tree.Children.Single().Children.Count);
    }
}
