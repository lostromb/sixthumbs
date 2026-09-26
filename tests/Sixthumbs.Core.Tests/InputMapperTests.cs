using Sixthumbs.Core;

namespace Sixthumbs.Core.Tests;

public class InputMapperTests
{
    [Fact]
    public void Disabled_destination_is_zero()
    {
        var mapping = new SourceMapping();
        mapping.GetOrCreate(Xbox360Control.A).Enabled = false;
        var source = new Xbox360State();
        source.SetButton(Xbox360Control.A, true);
        source.SetButton(Xbox360Control.B, true);

        var mapped = InputMapper.Apply(source, mapping);

        Assert.False(mapped.GetButton(Xbox360Control.A));
        Assert.True(mapped.GetButton(Xbox360Control.B));
    }

    [Fact]
    public void Remap_moves_face_button()
    {
        var mapping = new SourceMapping();
        mapping.GetOrCreate(Xbox360Control.B).Enabled = false;
        mapping.GetOrCreate(Xbox360Control.A).Source = Xbox360Control.B;
        var source = new Xbox360State();
        source.SetButton(Xbox360Control.B, true);

        var mapped = InputMapper.Apply(source, mapping);

        Assert.True(mapped.GetButton(Xbox360Control.A));
        Assert.False(mapped.GetButton(Xbox360Control.B));
    }

    [Fact]
    public void Left_stick_only_mask()
    {
        var mapping = new SourceMapping();
        foreach (var control in Xbox360Controls.All)
        {
            mapping.GetOrCreate(control).Enabled =
                control is Xbox360Control.LeftX or Xbox360Control.LeftY;
        }

        var source = new Xbox360State
        {
            LeftX = 111,
            RightX = 222,
            LeftTrigger = 200,
        };
        source.SetButton(Xbox360Control.A, true);

        var mapped = InputMapper.Apply(source, mapping);

        Assert.Equal(111, mapped.LeftX);
        Assert.Equal(0, mapped.RightX);
        Assert.Equal(0, mapped.LeftTrigger);
        Assert.False(mapped.GetButton(Xbox360Control.A));
    }

    [Fact]
    public void Mute_zeros_everything()
    {
        var mapping = new SourceMapping { Muted = true };
        var source = new Xbox360State { LeftX = 99 };
        source.SetButton(Xbox360Control.Y, true);

        var mapped = InputMapper.Apply(source, mapping);

        Assert.Equal(default, mapped);
    }

    [Fact]
    public void Axis_invert_and_deadzone()
    {
        var mapping = new SourceMapping();
        var binding = mapping.GetOrCreate(Xbox360Control.LeftX);
        binding.Invert = true;
        binding.Deadzone = 0.5f;

        var inside = InputMapper.Apply(new Xbox360State { LeftX = 100 }, mapping);
        Assert.Equal(0, inside.LeftX);

        var outside = InputMapper.Apply(new Xbox360State { LeftX = 20000 }, mapping);
        Assert.Equal(-20000, outside.LeftX);
    }

    [Fact]
    public void Joystick_layout_fills_canonical_state()
    {
        var layout = new JoystickLayout();
        var axes = new short[] { 100, -200, 300, -400, short.MaxValue, 0 };
        var buttons = new[] { true, false, false, false, false, false, false, false, false, false, false };
        var hats = new[] { Xbox360Buttons.DpadUp };

        var state = InputMapper.FromJoystick(axes, buttons, hats, layout);

        Assert.Equal(100, state.LeftX);
        Assert.Equal(200, state.LeftY);
        Assert.True(state.GetButton(Xbox360Control.A));
        Assert.True(state.GetButton(Xbox360Control.DpadUp));
        Assert.True(state.LeftTrigger > 200);
    }

    [Fact]
    public void Joystick_none_destination_is_ignored()
    {
        var layout = new JoystickLayout
        {
            Axes = [new JoystickAxisBinding { AxisIndex = 0, Destination = null }],
            Buttons = [new JoystickButtonBinding { ButtonIndex = 0, Destination = null }],
            Hats = [],
        };
        var axes = new short[] { short.MaxValue };
        var buttons = new[] { true };

        var state = InputMapper.FromJoystick(axes, buttons, [], layout);

        Assert.Equal(0, state.LeftX);
        Assert.False(state.GetButton(Xbox360Control.A));
    }
}
