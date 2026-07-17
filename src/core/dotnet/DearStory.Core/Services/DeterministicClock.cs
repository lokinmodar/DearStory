namespace DearStory.Core.Services;

/// <summary>
/// Provides an explicitly advanced deterministic clock for one story session.
/// </summary>
public sealed class DeterministicClock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeterministicClock" /> class.
    /// </summary>
    /// <param name="initialUtc">An initial UTC clock value.</param>
    public DeterministicClock(DateTimeOffset initialUtc)
    {
        CurrentUtc = initialUtc;
    }

    /// <summary>
    /// Gets the current deterministic UTC time.
    /// </summary>
    /// <value>The current deterministic UTC time.</value>
    public DateTimeOffset CurrentUtc { get; private set; }

    /// <summary>
    /// Advances the clock by a requested duration.
    /// </summary>
    /// <param name="delta">A duration to add to the current time.</param>
    public void Advance(TimeSpan delta) => CurrentUtc = CurrentUtc.Add(delta);

    /// <summary>
    /// Resets the clock to a requested UTC time.
    /// </summary>
    /// <param name="value">A UTC time value.</param>
    public void Reset(DateTimeOffset value) => CurrentUtc = value;
}
