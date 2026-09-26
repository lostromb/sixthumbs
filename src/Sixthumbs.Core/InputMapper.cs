namespace Sixthumbs.Core;

public static class InputMapper
{
    public static Xbox360State Apply(Xbox360State source, SourceMapping mapping)
    {
        if (mapping.Muted)
        {
            return default;
        }

        var dest = new Xbox360State();
        foreach (var control in Xbox360Controls.All)
        {
            var binding = mapping.GetOrCreate(control);
            if (!binding.Enabled)
            {
                continue;
            }

            WriteControl(ref dest, control, ReadControl(source, binding), binding.Source);
        }

        return dest;
    }

    public static int ReadControl(Xbox360State source, ControlBinding binding)
    {
        var raw = ReadControl(source, binding.Source);
        if (binding.Source.IsButton())
        {
            return raw;
        }

        if (binding.Source.IsTrigger())
        {
            var value = ApplyDeadzoneByte((byte)raw, binding.Deadzone);
            if (binding.Invert)
            {
                value = (byte)(255 - value);
            }

            return value;
        }

        var axis = ApplyDeadzoneShort((short)raw, binding.Deadzone);
        if (binding.Invert)
        {
            axis = axis == short.MinValue ? short.MaxValue : (short)-axis;
        }

        return axis;
    }

    public static int ReadControl(Xbox360State source, Xbox360Control control)
    {
        if (control.IsButton())
        {
            return source.GetButton(control) ? 1 : 0;
        }

        if (control.IsTrigger())
        {
            return source.GetTrigger(control);
        }

        return source.GetAxis(control);
    }

    public static void WriteControl(ref Xbox360State dest, Xbox360Control control, int value) =>
        WriteControl(ref dest, control, value, control);

    public static void WriteControl(
        ref Xbox360State dest,
        Xbox360Control control,
        int value,
        Xbox360Control source)
    {
        if (control.IsButton())
        {
            dest.SetButton(control, value != 0);
            return;
        }

        if (control.IsTrigger())
        {
            dest.SetTrigger(control, source.IsButton()
                ? value != 0 ? (byte)255 : (byte)0
                : (byte)Math.Clamp(value, 0, 255));
            return;
        }

        dest.SetAxis(control, source.IsButton()
            ? value != 0 ? short.MaxValue : (short)0
            : (short)Math.Clamp(value, short.MinValue, short.MaxValue));
    }

    public static byte ApplyDeadzoneByte(byte value, float deadzone)
    {
        if (deadzone <= 0)
        {
            return value;
        }

        var threshold = (int)(deadzone * 255);
        return value <= threshold ? (byte)0 : value;
    }

    public static short ApplyDeadzoneShort(short value, float deadzone)
    {
        if (deadzone <= 0)
        {
            return value;
        }

        var threshold = (int)(deadzone * short.MaxValue);
        return Math.Abs((int)value) <= threshold ? (short)0 : value;
    }

    public static Xbox360State FromJoystick(
        IReadOnlyList<short> axes,
        IReadOnlyList<bool> buttons,
        IReadOnlyList<Xbox360Buttons> hats,
        JoystickLayout layout)
    {
        var state = new Xbox360State();
        foreach (var axis in layout.Axes)
        {
            if (axis.AxisIndex < 0 || axis.AxisIndex >= axes.Count)
            {
                continue;
            }

            var value = axes[axis.AxisIndex];
            if (axis.Invert)
            {
                value = value == short.MinValue ? short.MaxValue : (short)-value;
            }

            if (axis.Destination is not { } dest)
            {
                continue;
            }

            if (dest.IsTrigger())
            {
                var trigger = (byte)Math.Clamp((value + 32768) * 255 / 65535, 0, 255);
                state.SetTrigger(dest, trigger);
            }
            else if (dest.IsAxis())
            {
                state.SetAxis(dest, value);
            }
        }

        foreach (var button in layout.Buttons)
        {
            if (button.ButtonIndex < 0 || button.ButtonIndex >= buttons.Count)
            {
                continue;
            }

            if (!buttons[button.ButtonIndex] || button.Destination is not { } dest)
            {
                continue;
            }

            if (dest.IsButton())
            {
                state.SetButton(dest, true);
            }
            else if (dest.IsTrigger())
            {
                state.SetTrigger(dest, 255);
            }
            else if (dest.IsAxis())
            {
                state.SetAxis(dest, short.MaxValue);
            }
        }

        foreach (var hat in layout.Hats)
        {
            if (hat.HatIndex < 0 || hat.HatIndex >= hats.Count)
            {
                continue;
            }

            state.Buttons |= (ushort)hats[hat.HatIndex];
        }

        return state;
    }
}
