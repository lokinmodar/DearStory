using DearStory.Sdk;
using Xunit;

public sealed class PackageConsumptionTests
{
    [Fact]
    public void Story_type_is_loadable_from_packaged_sdk()
    {
        Assert.Equal("ButtonStories", typeof(ButtonStories).Name);
    }

    [Fact]
    public void Packaged_generator_registers_the_story()
    {
        var registry = GeneratedStoryRegistryFactory.Create();

        var registration = Assert.Single(registry.Registrations);
        Assert.Equal("buttons/package-smoke", registration.Descriptor.Id.Value);
        Assert.Equal("label", Assert.Single(registration.Arguments).Name);
    }
}
