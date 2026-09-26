using Sixthumbs.Core;

namespace Sixthumbs.Core.Tests;

public class MappingPresetTests
{
    [Fact]
    public void Preset_roundtrips_application_mapping_only()
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
        Assert.DoesNotContain("joystick", json, StringComparison.OrdinalIgnoreCase);

        var applied = new SourceMapping { Muted = true };
        applied.Joystick = new JoystickLayout
        {
            Axes = [new JoystickAxisBinding { AxisIndex = 7, Destination = Xbox360Control.RightY }],
        };
        MappingPresetStore.Read(json).ApplyTo(applied);

        Assert.True(applied.Muted);
        Assert.False(applied.GetOrCreate(Xbox360Control.A).Enabled);
        Assert.Equal(Xbox360Control.X, applied.GetOrCreate(Xbox360Control.B).Source);
        Assert.True(applied.GetOrCreate(Xbox360Control.LeftX).Invert);
        Assert.Equal(0.25f, applied.GetOrCreate(Xbox360Control.LeftX).Deadzone);
        Assert.Equal(7, applied.Joystick!.Axes[0].AxisIndex);
        Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_does_not_share_binding_instances()
    {
        var mapping = new SourceMapping();
        mapping.GetOrCreate(Xbox360Control.Y).Enabled = false;
        var clone = mapping.CloneApplicationBindings();
        clone.GetOrCreate(Xbox360Control.Y).Enabled = true;
        Assert.False(mapping.GetOrCreate(Xbox360Control.Y).Enabled);
    }

    [Fact]
    public void Raw_layout_gains_missing_hardware_buttons()
    {
        var layout = new JoystickLayout();
        Assert.Equal(11, layout.Buttons.Count);

        layout.EnsureHardwareCoverage(axisCount: 6, buttonCount: 12, hatCount: 1);

        Assert.Equal(12, layout.Buttons.Count);
        Assert.Contains(layout.Buttons, b => b.ButtonIndex == 11);
        var saved = layout.Buttons.First(b => b.ButtonIndex == 11);
        saved.Destination = Xbox360Control.Start;

        layout.EnsureHardwareCoverage(12, 12, 1);
        Assert.Equal(Xbox360Control.Start, layout.Buttons.First(b => b.ButtonIndex == 11).Destination);
        Assert.Equal(12, layout.ButtonsForDevice(12).Count());
    }

    [Fact]
    public void Mapping_resets_to_defaults_preserving_joystick_bindings()
    {
        var mapping = new SourceMapping();
        mapping.GetOrCreate(Xbox360Control.A).Enabled = false;
        mapping.Joystick = new JoystickLayout
        {
            Axes = [new JoystickAxisBinding { AxisIndex = 3, Destination = Xbox360Control.LeftX }],
        };

        mapping.ResetToDefaults();

        Assert.True(mapping.GetOrCreate(Xbox360Control.A).Enabled);
        Assert.Equal(Xbox360Control.A, mapping.GetOrCreate(Xbox360Control.A).Source);
        Assert.Equal(3, mapping.Joystick!.Axes[0].AxisIndex);
    }
}
