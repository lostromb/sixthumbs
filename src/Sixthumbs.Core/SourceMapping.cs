namespace Sixthumbs.Core;

public sealed class ControlBinding
{
    public bool Enabled { get; set; } = true;
    public Xbox360Control Source { get; set; }
    public bool Invert { get; set; }
    public float Deadzone { get; set; }
}

public sealed class JoystickAxisBinding
{
    public int AxisIndex { get; set; }
    public Xbox360Control Destination { get; set; }
    public bool Invert { get; set; }
}

public sealed class JoystickButtonBinding
{
    public int ButtonIndex { get; set; }
    public Xbox360Control Destination { get; set; }
}

public sealed class JoystickHatBinding
{
    public int HatIndex { get; set; }
}

public sealed class JoystickLayout
{
    public List<JoystickAxisBinding> Axes { get; set; } = CreateDefaultAxes();
    public List<JoystickButtonBinding> Buttons { get; set; } = CreateDefaultButtons();
    public List<JoystickHatBinding> Hats { get; set; } = [new JoystickHatBinding { HatIndex = 0 }];

    public static List<JoystickAxisBinding> CreateDefaultAxes() =>
    [
        new() { AxisIndex = 0, Destination = Xbox360Control.LeftX },
        new() { AxisIndex = 1, Destination = Xbox360Control.LeftY, Invert = true },
        new() { AxisIndex = 2, Destination = Xbox360Control.RightX },
        new() { AxisIndex = 3, Destination = Xbox360Control.RightY, Invert = true },
        new() { AxisIndex = 4, Destination = Xbox360Control.LeftTrigger },
        new() { AxisIndex = 5, Destination = Xbox360Control.RightTrigger },
    ];

    public static List<JoystickButtonBinding> CreateDefaultButtons() =>
    [
        new() { ButtonIndex = 0, Destination = Xbox360Control.A },
        new() { ButtonIndex = 1, Destination = Xbox360Control.B },
        new() { ButtonIndex = 2, Destination = Xbox360Control.X },
        new() { ButtonIndex = 3, Destination = Xbox360Control.Y },
        new() { ButtonIndex = 4, Destination = Xbox360Control.LeftShoulder },
        new() { ButtonIndex = 5, Destination = Xbox360Control.RightShoulder },
        new() { ButtonIndex = 6, Destination = Xbox360Control.Back },
        new() { ButtonIndex = 7, Destination = Xbox360Control.Start },
        new() { ButtonIndex = 8, Destination = Xbox360Control.LeftStick },
        new() { ButtonIndex = 9, Destination = Xbox360Control.RightStick },
        new() { ButtonIndex = 10, Destination = Xbox360Control.Guide },
    ];
}

public sealed class SourceMapping
{
    public bool Muted { get; set; }
    public Dictionary<Xbox360Control, ControlBinding> Destinations { get; set; } = CreateIdentity();
    public JoystickLayout? Joystick { get; set; }

    public static Dictionary<Xbox360Control, ControlBinding> CreateIdentity()
    {
        var map = new Dictionary<Xbox360Control, ControlBinding>();
        foreach (var control in Xbox360Controls.All)
        {
            map[control] = new ControlBinding
            {
                Enabled = true,
                Source = control,
            };
        }

        return map;
    }

    public ControlBinding GetOrCreate(Xbox360Control destination)
    {
        if (!Destinations.TryGetValue(destination, out var binding))
        {
            binding = new ControlBinding { Enabled = true, Source = destination };
            Destinations[destination] = binding;
        }

        return binding;
    }

    public void ResetToDefaults()
    {
        Destinations = CreateIdentity();
        if (Joystick is not null)
        {
            Joystick = new JoystickLayout();
        }
    }

    public SourceMapping CloneBindings()
    {
        var copy = new SourceMapping();
        copy.ReplaceBindings(this);
        copy.Muted = false;
        return copy;
    }

    public void ReplaceBindings(SourceMapping other)
    {
        Destinations.Clear();
        foreach (var control in Xbox360Controls.All)
        {
            if (other.Destinations.TryGetValue(control, out var binding))
            {
                Destinations[control] = new ControlBinding
                {
                    Enabled = binding.Enabled,
                    Source = binding.Source,
                    Invert = binding.Invert,
                    Deadzone = binding.Deadzone,
                };
            }
            else
            {
                Destinations[control] = new ControlBinding
                {
                    Enabled = true,
                    Source = control,
                };
            }
        }

        Joystick = other.Joystick is null ? null : CloneJoystick(other.Joystick);
    }

    private static JoystickLayout CloneJoystick(JoystickLayout source) =>
        new()
        {
            Axes = source.Axes.Select(a => new JoystickAxisBinding
            {
                AxisIndex = a.AxisIndex,
                Destination = a.Destination,
                Invert = a.Invert,
            }).ToList(),
            Buttons = source.Buttons.Select(b => new JoystickButtonBinding
            {
                ButtonIndex = b.ButtonIndex,
                Destination = b.Destination,
            }).ToList(),
            Hats = source.Hats.Select(h => new JoystickHatBinding { HatIndex = h.HatIndex }).ToList(),
        };
}
