namespace DearStory.Sdk;

/// <summary>
/// Marks one public argument property as part of the DearStory argument contract.
/// </summary>
/// <param name="name">The serialized argument name.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class StoryArgAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the serialized argument name.
    /// </summary>
    /// <value>The serialized argument name.</value>
    public string Name { get; } = name;
}
