using System.Text.Json.Nodes;
using DearStory.Core.Events;
using DearStory.Core.Sessions;
using DearStory.Core.Services;
using DearStory.Core.Targets;

namespace DearStory.Sdk;

/// <summary>
/// Exposes the DearStory core session state and emitted artifacts to one story callback.
/// </summary>
public sealed class StoryContext
{
    private readonly List<ActionEvent> _actions = [];
    private readonly List<LogEvent> _logs = [];
    private readonly List<InteractionTarget> _targets = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StoryContext" /> class.
    /// </summary>
    /// <param name="session">The active story session.</param>
    public StoryContext(StorySession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Gets the active story session.
    /// </summary>
    /// <value>The active story session.</value>
    public StorySession Session { get; }

    /// <summary>
    /// Gets the active serializable arguments.
    /// </summary>
    /// <value>The active serializable arguments.</value>
    public JsonNode Args => Session.Arguments;

    /// <summary>
    /// Gets the collected action events for the current callback execution.
    /// </summary>
    /// <value>The collected action events.</value>
    public IList<ActionEvent> Actions => _actions;

    /// <summary>
    /// Gets the collected log events for the current callback execution.
    /// </summary>
    /// <value>The collected log events.</value>
    public IList<LogEvent> Logs => _logs;

    /// <summary>
    /// Gets the collected interaction targets for the current callback execution.
    /// </summary>
    /// <value>The collected interaction targets.</value>
    public IList<InteractionTarget> Targets => _targets;

    /// <summary>
    /// Gets the deterministic clock for the current session.
    /// </summary>
    /// <value>The deterministic clock.</value>
    public DeterministicClock Clock => Session.Clock;

    /// <summary>
    /// Gets the deterministic random source for the current session.
    /// </summary>
    /// <value>The deterministic random source.</value>
    public DeterministicRandom Random => Session.Random;
}
