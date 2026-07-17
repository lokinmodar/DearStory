using DearStory.Transport.Windows;
using System.Runtime.Versioning;
using Xunit;

namespace DearStory.Transport.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class SharedMemoryFrameChannelTests
{
    [Fact]
    public void Publish_then_read_latest_returns_written_rgba_frame()
    {
        var descriptor = FrameTransportDescriptor.Create("Local\\dearstory-frame-test", width: 2, height: 2, stride: 8, slotCount: 3);
        using var writer = new SharedMemoryFrameWriter(descriptor);
        using var reader = new SharedMemoryFrameReader(descriptor);

        writer.Publish([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]);

        Assert.True(reader.TryReadLatest(out var frame));
        Assert.Equal(1L, frame.Sequence);
        Assert.Equal(16, frame.Bytes.Length);
    }
}
