using Sixthumbs.Core;

namespace Sixthumbs.Core.Tests;

public class ProtocolTests
{
    [Fact]
    public void Packed_state_roundtrips()
    {
        var original = new Xbox360State
        {
            Buttons = (ushort)(Xbox360Buttons.A | Xbox360Buttons.DpadLeft),
            LeftTrigger = 12,
            RightTrigger = 250,
            LeftX = -1,
            LeftY = 32767,
            RightX = short.MinValue,
            RightY = 42,
        };

        Span<byte> buffer = stackalloc byte[Xbox360State.PackedSize];
        original.WritePacked(buffer);
        var restored = Xbox360State.ReadPacked(buffer);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Handshake_roundtrips()
    {
        var bytes = Protocol.WriteHandshake("secret", "Pad One");
        Assert.True(Protocol.TryReadHandshake(bytes, out var hash, out var name, out var error));
        Assert.Null(error);
        Assert.Equal("Pad One", name);
        Assert.True(Protocol.PasswordMatches("secret", hash));
        Assert.False(Protocol.PasswordMatches("wrong", hash));
    }

    [Fact]
    public void Empty_host_password_accepts_any_client()
    {
        Assert.True(Protocol.PasswordMatches("", Protocol.HashPassword("anything")));
    }

    [Fact]
    public void State_frame_roundtrips()
    {
        var state = new Xbox360State { LeftX = 1234 };
        state.SetButton(Xbox360Control.Start, true);
        var frame = Protocol.WriteStateFrame(99, state);

        var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(frame);
        Assert.Equal((uint)Protocol.StatePayloadSize, length);
        var payload = frame.AsSpan(4);
        Assert.True(Protocol.TryReadStatePayload(payload, out var sequence, out var restored));
        Assert.Equal(99u, sequence);
        Assert.Equal(state, restored);
    }

    [Fact]
    public void Rejects_bad_magic()
    {
        var bytes = Protocol.WriteHandshake("", "x");
        bytes[0] ^= 0xFF;
        Assert.False(Protocol.TryReadHandshake(bytes, out _, out _, out var error));
        Assert.Contains("magic", error, StringComparison.OrdinalIgnoreCase);
    }
}

public class MixerEngineTests
{
    [Fact]
    public void Tick_maps_then_merges_then_submits()
    {
        var padA = new SnapshotInputSource("a", "A");
        var padB = new SnapshotInputSource("b", "B");
        var stateA = new Xbox360State();
        stateA.SetButton(Xbox360Control.A, true);
        padA.Update(stateA);
        padB.Update(new Xbox360State { LeftX = -9 });

        var mappings = new Dictionary<string, SourceMapping>
        {
            ["a"] = new SourceMapping(),
            ["b"] = new SourceMapping(),
        };
        mappings["b"].GetOrCreate(Xbox360Control.LeftX).Enabled = true;

        var captured = new Xbox360State();
        var sink = new CapturingSink(s => captured = s);
        using var engine = new MixerEngine(
            () => [padA, padB],
            id => mappings[id],
            sink: sink);

        engine.Tick();

        Assert.True(captured.GetButton(Xbox360Control.A));
        Assert.Equal(-9, captured.LeftX);
        Assert.Equal(captured, engine.LastMerged);
    }

    private sealed class CapturingSink : IOutputSink
    {
        private readonly Action<Xbox360State> _onSubmit;

        public CapturingSink(Action<Xbox360State> onSubmit) => _onSubmit = onSubmit;

        public void Submit(Xbox360State merged) => _onSubmit(merged);

        public void Dispose()
        {
        }
    }
}
