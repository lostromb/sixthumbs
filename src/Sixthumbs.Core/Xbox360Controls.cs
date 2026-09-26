namespace Sixthumbs.Core;

[Flags]
public enum Xbox360Buttons : ushort
{
    None = 0,
    DpadUp = 0x0001,
    DpadDown = 0x0002,
    DpadLeft = 0x0004,
    DpadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftStick = 0x0040,
    RightStick = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

public enum Xbox360Control
{
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    Back,
    Start,
    Guide,
    LeftStick,
    RightStick,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
    LeftTrigger,
    RightTrigger,
    LeftX,
    LeftY,
    RightX,
    RightY,
}

public static class Xbox360Controls
{
    public static readonly Xbox360Control[] All = Enum.GetValues<Xbox360Control>();

    public static readonly Xbox360Control[] Buttons =
    [
        Xbox360Control.A, Xbox360Control.B, Xbox360Control.X, Xbox360Control.Y,
        Xbox360Control.LeftShoulder, Xbox360Control.RightShoulder,
        Xbox360Control.Back, Xbox360Control.Start, Xbox360Control.Guide,
        Xbox360Control.LeftStick, Xbox360Control.RightStick,
        Xbox360Control.DpadUp, Xbox360Control.DpadDown, Xbox360Control.DpadLeft, Xbox360Control.DpadRight,
    ];

    public static readonly Xbox360Control[] Analogs =
    [
        Xbox360Control.LeftTrigger, Xbox360Control.RightTrigger,
        Xbox360Control.LeftX, Xbox360Control.LeftY, Xbox360Control.RightX, Xbox360Control.RightY,
    ];

    public static bool IsButton(this Xbox360Control control) =>
        control is not (Xbox360Control.LeftTrigger or Xbox360Control.RightTrigger
            or Xbox360Control.LeftX or Xbox360Control.LeftY
            or Xbox360Control.RightX or Xbox360Control.RightY);

    public static bool IsAxis(this Xbox360Control control) =>
        control is Xbox360Control.LeftX or Xbox360Control.LeftY
            or Xbox360Control.RightX or Xbox360Control.RightY;

    public static bool IsTrigger(this Xbox360Control control) =>
        control is Xbox360Control.LeftTrigger or Xbox360Control.RightTrigger;

    public static Xbox360Buttons ToButtonFlag(this Xbox360Control control) => control switch
    {
        Xbox360Control.A => Xbox360Buttons.A,
        Xbox360Control.B => Xbox360Buttons.B,
        Xbox360Control.X => Xbox360Buttons.X,
        Xbox360Control.Y => Xbox360Buttons.Y,
        Xbox360Control.LeftShoulder => Xbox360Buttons.LeftShoulder,
        Xbox360Control.RightShoulder => Xbox360Buttons.RightShoulder,
        Xbox360Control.Back => Xbox360Buttons.Back,
        Xbox360Control.Start => Xbox360Buttons.Start,
        Xbox360Control.Guide => Xbox360Buttons.Guide,
        Xbox360Control.LeftStick => Xbox360Buttons.LeftStick,
        Xbox360Control.RightStick => Xbox360Buttons.RightStick,
        Xbox360Control.DpadUp => Xbox360Buttons.DpadUp,
        Xbox360Control.DpadDown => Xbox360Buttons.DpadDown,
        Xbox360Control.DpadLeft => Xbox360Buttons.DpadLeft,
        Xbox360Control.DpadRight => Xbox360Buttons.DpadRight,
        _ => Xbox360Buttons.None,
    };

    public static string DisplayName(this Xbox360Control control) => control switch
    {
        Xbox360Control.LeftShoulder => "LB",
        Xbox360Control.RightShoulder => "RB",
        Xbox360Control.LeftStick => "LS",
        Xbox360Control.RightStick => "RS",
        Xbox360Control.LeftTrigger => "LT",
        Xbox360Control.RightTrigger => "RT",
        Xbox360Control.LeftX => "Left X",
        Xbox360Control.LeftY => "Left Y",
        Xbox360Control.RightX => "Right X",
        Xbox360Control.RightY => "Right Y",
        Xbox360Control.DpadUp => "D-pad Up",
        Xbox360Control.DpadDown => "D-pad Down",
        Xbox360Control.DpadLeft => "D-pad Left",
        Xbox360Control.DpadRight => "D-pad Right",
        _ => control.ToString(),
    };
}
