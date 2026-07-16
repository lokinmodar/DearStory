using System.Buffers;

namespace DearStory.Protocol.Tests;

internal sealed class RecordingArrayPool<T> : ArrayPool<T>
{
    private readonly ArrayPool<T> _inner = ArrayPool<T>.Shared;

    internal List<int> RequestedLengths { get; } = [];

    public override T[] Rent(int minimumLength)
    {
        RequestedLengths.Add(minimumLength);
        return _inner.Rent(minimumLength);
    }

    public override void Return(T[] array, bool clearArray = false) => _inner.Return(array, clearArray);
}
