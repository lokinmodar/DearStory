using System.Text.Json;

namespace DearStory.Protocol.Tests;

internal static class JsonSemanticComparer
{
    internal static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        using var leftDocument = JsonDocument.Parse(left.ToArray());
        using var rightDocument = JsonDocument.Parse(right.ToArray());
        return JsonElement.DeepEquals(leftDocument.RootElement, rightDocument.RootElement);
    }
}
