using DearStory.Sdk;
using Xunit;

namespace DearStory.Sdk.Tests;

public sealed class ReflectionStoryRegistryTests
{
    [Fact]
    public void Reflection_registry_requires_explicit_opt_in()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ReflectionStoryRegistry.Create(
                typeof(ReflectionStoryRegistryTests).Assembly,
                new ReflectionStoryRegistryOptions
                {
                    AllowReflectionFallback = false,
                }));
    }
}
