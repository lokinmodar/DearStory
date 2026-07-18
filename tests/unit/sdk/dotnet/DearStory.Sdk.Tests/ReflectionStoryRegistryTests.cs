using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using DearStory.Core;
using DearStory.Core.Events;
using DearStory.Core.Schemas;
using DearStory.Core.Sessions;
using DearStory.Core.Targets;
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

    [Fact]
    public void Reflection_registry_rejects_null_inputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReflectionStoryRegistry.Create(
                null!,
                new ReflectionStoryRegistryOptions
                {
                    AllowReflectionFallback = true,
                }));

        Assert.Throws<ArgumentNullException>(() =>
            ReflectionStoryRegistry.Create(
                typeof(ReflectionStoryRegistryTests).Assembly,
                null!));
    }

    [Fact]
    public void Reflection_registry_builds_sorted_registrations_and_executes_callbacks()
    {
        var registry = ReflectionStoryRegistry.Create(
            typeof(ReflectionStoryRegistryTests).Assembly,
            new ReflectionStoryRegistryOptions
            {
                AllowReflectionFallback = true,
            });

        var registrations = registry.Registrations
            .Where(static registration => registration.Descriptor.Id.Value.StartsWith("buttons/", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(
            ["buttons/primary", "buttons/primarymanaged", "buttons/secondary"],
            registrations.Select(static registration => registration.Descriptor.Id.Value));

        var primary = registrations.Single(static registration => registration.Descriptor.Id.Value == "buttons/primary");
        Assert.Equal("Primary", primary.Descriptor.Title);
        Assert.Equal(["Buttons"], primary.Descriptor.Hierarchy);
        Assert.Equal("Save", primary.DefaultArguments["label"]!.GetValue<string>());
        Assert.True(primary.DefaultArguments["disabled"]!.GetValue<bool>());
        Assert.Equal(3, primary.DefaultArguments["count"]!.GetValue<int>());
        Assert.Equal((short)7, primary.DefaultArguments["shortValue"]!.GetValue<short>());
        Assert.Equal(42L, primary.DefaultArguments["longValue"]!.GetValue<long>());
        Assert.Equal(1.5d, primary.DefaultArguments["ratio"]!.GetValue<double>());
        Assert.Equal(0.25f, primary.DefaultArguments["opacity"]!.GetValue<float>());
        Assert.Equal(9.99m, primary.DefaultArguments["price"]!.GetValue<decimal>());
        Assert.Equal("Secondary", primary.DefaultArguments["tone"]!.GetValue<string>());
        Assert.DoesNotContain(primary.Arguments, static argument => argument.Name == "ignored");
        Assert.Equal("string", primary.Arguments.Single(static argument => argument.Name == "tone").Schema["type"]!.GetValue<string>());
        Assert.Equal(["Primary", "Secondary"], primary.Arguments.Single(static argument => argument.Name == "tone").Schema["enum"]!.AsArray().Select(static item => item!.GetValue<string>()));

        var session = StorySession.Open(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            StoryId.Parse("buttons/primary"),
            primary.DefaultArguments.DeepClone(),
            42,
            DateTimeOffset.Parse("2026-07-17T12:00:00Z"));

        var context = new StoryContext(session);
        primary.Render(context);

        Assert.Same(session, context.Session);
        Assert.Same(session.Arguments, context.Args);
        Assert.Same(session.Clock, context.Clock);
        Assert.Same(session.Random, context.Random);
        Assert.Single(context.Actions);
        Assert.Single(context.Logs);
        Assert.Single(context.Targets);
        Assert.Equal("button.clicked", context.Actions[0].Name);
        Assert.Equal("info", context.Logs[0].Level);
        Assert.Equal("primary-button", context.Targets[0].Id);
        Assert.Equal("button", context.Targets[0].Semantic!.Role);
        Assert.Equal("Primary action", context.Targets[0].Semantic!.AccessibleName);
    }

    [Fact]
    public void Reflection_builder_maps_supported_property_types()
    {
        var schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(string), "Save")!;
        Assert.Equal("string", schema["type"]!.GetValue<string>());
        Assert.Equal("Save", schema["default"]!.GetValue<string>());

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(bool), true)!;
        Assert.Equal("boolean", schema["type"]!.GetValue<string>());
        Assert.True(schema["default"]!.GetValue<bool>());

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(int), 3)!;
        Assert.Equal("integer", schema["type"]!.GetValue<string>());
        Assert.Equal(3L, schema["default"]!.GetValue<long>());

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(long), 42L)!;
        Assert.Equal("integer", schema["type"]!.GetValue<string>());
        Assert.Equal(42L, schema["default"]!.GetValue<long>());

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(short), (short)7)!;
        Assert.Equal("integer", schema["type"]!.GetValue<string>());
        Assert.Equal(7L, schema["default"]!.GetValue<long>());

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(float), 0.25f)!;
        Assert.Equal("number", schema["type"]!.GetValue<string>());
        Assert.Equal(0.25d, schema["default"]!.GetValue<double>(), 6);

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(double), 1.5d)!;
        Assert.Equal("number", schema["type"]!.GetValue<string>());
        Assert.Equal(1.5d, schema["default"]!.GetValue<double>(), 6);

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(decimal), 9.99m)!;
        Assert.Equal("number", schema["type"]!.GetValue<string>());
        Assert.Equal(9.99d, schema["default"]!.GetValue<double>(), 6);

        schema = (JsonObject)InvokeBuilderMethod("BuildPropertySchema", typeof(SampleTone), SampleTone.Secondary)!;
        Assert.Equal("string", schema["type"]!.GetValue<string>());
        Assert.Equal(["Primary", "Secondary"], schema["enum"]!.AsArray().Select(static item => item!.GetValue<string>()));
        Assert.Equal("Secondary", schema["default"]!.GetValue<string>());
    }

    [Fact]
    public void Reflection_builder_rejects_unsupported_property_types_and_defaults()
    {
        Assert.Throws<NotSupportedException>(() => InvokeBuilderMethod("BuildPropertySchema", typeof(object), new object()));
        Assert.Throws<NotSupportedException>(() => InvokeBuilderMethod("ToJsonNode", new object()));
    }

    [Fact]
    public void Generated_registry_descriptors_sort_and_metadata_records_round_trip()
    {
        var storyAttribute = new StoryAttribute("buttons/primary", typeof(SampleStoryArgs));
        var storyArgAttribute = new StoryArgAttribute("label");

        var registry = new GeneratedStoryRegistry
        {
            Registrations =
            [
                new GeneratedStoryRegistration
                {
                    Descriptor = StoryDescriptor.Create("buttons/secondary", "Secondary"),
                    ArgumentSchema = ArgumentSchema.Parse("""{"type":"object","properties":{}}"""),
                    DefaultArguments = new JsonObject(),
                    Arguments =
                    [
                        new GeneratedStoryArgument
                        {
                            Name = "label",
                            Schema = JsonNode.Parse("""{"type":"string"}""")!,
                            DefaultValue = JsonValue.Create("Save"),
                            Description = "Caption",
                        }
                    ],
                    Render = static _ => { },
                },
                new GeneratedStoryRegistration
                {
                    Descriptor = StoryDescriptor.Create("buttons/primary", "Primary"),
                    ArgumentSchema = ArgumentSchema.Parse("""{"type":"object","properties":{}}"""),
                    DefaultArguments = new JsonObject(),
                    Arguments = [],
                    Render = static _ => { },
                }
            ],
        };

        Assert.Equal("buttons/primary", storyAttribute.Id);
        Assert.Equal(typeof(SampleStoryArgs), storyAttribute.ArgsType);
        Assert.Equal("label", storyArgAttribute.Name);
        Assert.Equal(["buttons/primary", "buttons/secondary"], registry.Descriptors.Select(static descriptor => descriptor.Id.Value));
        Assert.Equal("label", registry.Registrations[0].Arguments[0].Name);
        Assert.Equal("Caption", registry.Registrations[0].Arguments[0].Description);
    }

    [Fact]
    public void Story_context_rejects_null_session()
        => Assert.Throws<ArgumentNullException>(() => new StoryContext(null!));

    private static object? InvokeBuilderMethod(string name, params object?[] arguments)
    {
        var builderType = typeof(ReflectionStoryRegistry).GetNestedType("ReflectionStoryRegistryBuilder", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The reflection builder type was not found.");
        var method = builderType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"The builder method '{name}' was not found.");

        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private enum SampleTone
    {
        Primary,
        Secondary,
    }

    private sealed class SampleStoryArgs
    {
        [StoryArg("label")]
        public string Label { get; init; } = "Save";

        [StoryArg("disabled")]
        public bool Disabled { get; init; } = true;

        [StoryArg("count")]
        public int Count { get; init; } = 3;

        [StoryArg("shortValue")]
        public short ShortValue { get; init; } = 7;

        [StoryArg("longValue")]
        public long LongValue { get; init; } = 42L;

        [StoryArg("ratio")]
        public double Ratio { get; init; } = 1.5D;

        [StoryArg("opacity")]
        public float Opacity { get; init; } = 0.25F;

        [StoryArg("price")]
        public decimal Price { get; init; } = 9.99M;

        [StoryArg("tone")]
        public SampleTone Tone { get; init; } = SampleTone.Secondary;

        public string Ignored { get; init; } = "ignored";
    }

    private static class SampleStories
    {
        [Story("Buttons\\Primary", typeof(SampleStoryArgs))]
        public static void Primary(StoryContext context)
        {
            context.Actions.Add(
                new ActionEvent
                {
                    Name = "button.clicked",
                    Payload = new JsonObject
                    {
                        ["label"] = context.Args["label"]!.GetValue<string>(),
                    },
                    EmittedAtUtc = context.Clock.CurrentUtc,
                    TargetId = "primary-button",
                });

            context.Logs.Add(
                new LogEvent
                {
                    Level = "info",
                    Message = "rendered",
                    EmittedAtUtc = context.Clock.CurrentUtc,
                    Details = new JsonObject
                    {
                        ["count"] = context.Args["count"]!.GetValue<int>(),
                    },
                });

            context.Targets.Add(
                new InteractionTarget
                {
                    Id = "primary-button",
                    Bounds = new JsonObject
                    {
                        ["x"] = 10,
                        ["y"] = 20,
                    },
                    Semantic = new InteractionTargetSemanticMetadata
                    {
                        Role = "button",
                        AccessibleName = "Primary action",
                        Description = "Commits the current form",
                    },
                });
        }

        [Story("buttons/secondary", typeof(SampleStoryArgs))]
        public static void Secondary(StoryContext context)
        {
            _ = context.Random.NextUInt32();
        }
    }
}
