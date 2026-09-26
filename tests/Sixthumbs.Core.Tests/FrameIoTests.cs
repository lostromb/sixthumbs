using Sixthumbs.Core;
using Sixthumbs.Net;

namespace Sixthumbs.Core.Tests;

public class FrameIoTests
{
    [Fact]
    public async Task Handshake_and_state_frame_over_stream()
    {
        using var stream = new MemoryStream();
        var handshake = Protocol.WriteHandshake("pw", "Room");
        await FrameIo.WriteAllAsync(stream, handshake, CancellationToken.None);
        stream.Position = 0;
        var read = await FrameIo.ReadHandshakeAsync(stream, CancellationToken.None);
        Assert.True(read.ok);
        Assert.Equal("Room", read.displayName);
        Assert.True(Protocol.PasswordMatches("pw", read.passwordHash));

        stream.SetLength(0);
        var state = new Xbox360State { RightY = 77 };
        await FrameIo.WriteAllAsync(stream, Protocol.WriteStateFrame(3, state), CancellationToken.None);
        stream.Position = 0;
        var payload = await FrameIo.ReadFrameAsync(stream, CancellationToken.None);
        Assert.NotNull(payload);
        Assert.True(Protocol.TryReadStatePayload(payload, out var seq, out var restored));
        Assert.Equal(3u, seq);
        Assert.Equal(state, restored);
    }
}
