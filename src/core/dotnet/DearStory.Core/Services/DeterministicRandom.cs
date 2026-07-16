namespace DearStory.Core.Services;

/// <summary>
/// Provides a deterministic pseudo-random sequence for one story session.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeterministicRandom" /> class.
    /// </summary>
    /// <param name="seed">An initial seed value.</param>
    public DeterministicRandom(long seed)
    {
        Reset(seed);
    }

    /// <summary>
    /// Resets the pseudo-random sequence to a requested seed.
    /// </summary>
    /// <param name="seed">A seed value.</param>
    public void Reset(long seed)
    {
        _state = unchecked((ulong)seed);
        if (_state == 0)
        {
            _state = 0x9E3779B97F4A7C15UL;
        }
    }

    /// <summary>
    /// Gets the next deterministic unsigned 32-bit value.
    /// </summary>
    /// <returns>An unsigned 32-bit pseudo-random value.</returns>
    public uint NextUInt32()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        var value = _state * 2685821657736338717UL;
        return unchecked((uint)(value >> 32));
    }
}
