using Sixthumbs.Core;

namespace Sixthumbs.Core.Tests;

public class MappingPresetTests
{
    [Fact]
    public void Roundtrips_enable_remap_and_joystick_layout()
    {
        var mapping = new SourceMapping();
        mapping.Muted = true;
        mapping.GetOrCreate(Xbox360Control.A).Enabled = false;
        mapping.GetOrCreate(Xbox360Control.B).Source = Xbox360Control.X;
        mapping.GetOrCreate(Xbox360Control.LeftX).Invert = true;
        mapping.GetOrCreate(Xbox360Control.LeftX).Deadzone = 0.25f;
        mapping.Joystick = new JoystickLayout
        {
            Axes = [new JoystickAxisBinding { AxisIndex = 2, Destination = Xbox360Control.LeftX, Invert = true }],
            Buttons = [new JoystickButtonBinding { ButtonIndex = 4, Destination = Xbox360Control.A }],
            Hats = [new JoystickHatBinding { HatIndex = 1 }],
        };

        var json = MappingPresetStore.Write(MappingPreset.From(mapping));
        var loaded = MappingPresetStore.Read(json);
        var applied = new SourceMapping { Muted = false };
        loaded.ApplyTo(applied);

        Assert.False(applied.Muted);
        Assert.False(applied.GetOrCreate(Xbox360Control.A).Enabled);
        Assert.Equal(Xbox360Control.X, applied.GetOrCreate(Xbox360Control.B).Source);
        Assert.True(applied.GetOrCreate(Xbox360Control.LeftX).Invert);
        Assert.Equal(0.25f, applied.GetOrCreate(Xbox360Control.LeftX).Deadzone);
        Assert.NotNull(applied.Joystick);
        Assert.Equal(2, applied.Joystick!.Axes[0].AxisIndex);
        Assert.Equal(Xbox360Control.LeftX, applied.Joystick.Axes[0].Destination);
        Assert.True(applied.Joystick.Axes[0].Invert);
        Assert.Equal(4, applied.Joystick.Buttons[0].ButtonIndex);
        Assert.Equal(1, applied.Joystick.Hats[0].HatIndex);
        Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_does_not_share_binding_instances()
    {
        var mapping = new SourceMapping();
        mapping.GetOrCreate(Xbox360Control.Y).Enabled = false;
        var clone = mapping.CloneBindings();
        clone.GetOrCreate(Xbox360Control.Y).Enabled = true;
        Assert.False(mapping.GetOrCreate(Xbox360Control.Y).Enabled);
    }
}
