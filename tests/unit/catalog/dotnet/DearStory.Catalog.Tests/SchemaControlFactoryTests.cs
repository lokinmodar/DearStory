using DearStory.Catalog.Controls;
using DearStory.Core.Schemas;
using Xunit;

namespace DearStory.Catalog.Tests;

public sealed class SchemaControlFactoryTests
{
    [Fact]
    public void Create_returns_color_editor_for_rgba_annotation()
    {
        var schema = ArgumentSchema.Parse(
            """
            { "type": "string", "format": "color", "x-dearstory-control": "color-rgba" }
            """);

        var control = SchemaControlFactory.Create(schema);

        Assert.Equal("color-rgba", control.Kind);
    }
}
