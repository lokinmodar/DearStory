namespace DearStory.Sdk;

/// <summary>
/// Marks one static method as a DearStory story entry point.
/// </summary>
/// <param name="id">The raw story identifier.</param>
/// <param name="argsType">The argument type used to describe schema and defaults.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class StoryAttribute(string id, Type argsType) : Attribute
{
    /// <summary>
    /// Gets the raw story identifier.
    /// </summary>
    /// <value>The raw story identifier.</value>
    public string Id { get; } = id;

    /// <summary>
    /// Gets the argument type used to describe schema and defaults.
    /// </summary>
    /// <value>The argument type.</value>
    public Type ArgsType { get; } = argsType;

    /// <summary>
    /// Gets or sets a value indicating whether the story is part of the canonical visual corpus by default.
    /// </summary>
    /// <value><see langword="true" /> when the story opts into the canonical visual corpus.</value>
    public bool IncludeInCanonicalCorpus { get; init; }
}
