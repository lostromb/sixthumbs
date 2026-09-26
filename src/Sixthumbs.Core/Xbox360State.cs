using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Sixthumbs.Core;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Xbox360State : IEquatable<Xbox360State>
{
    public const int PackedSize = 12;

    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short LeftX;
    public short LeftY;
    public short RightX;
    public short RightY;

    public Xbox360Buttons ButtonFlags => (Xbox360Buttons)Buttons;

    public bool GetButton(Xbox360Control control) =>
        control.IsButton() && (Buttons & (ushort)control.ToButtonFlag()) != 0;

    public void SetButton(Xbox360Control control, bool pressed)
    {
        var flag = (ushort)control.ToButtonFlag();
        if (flag == 0)
        {
            return;
        }

        if (pressed)
        {
            Buttons |= flag;
        }
        else
        {
            Buttons = (ushort)(Buttons & ~flag);
        }
    }

    public short GetAxis(Xbox360Control control) => control switch
    {
        Xbox360Control.LeftX => LeftX,
        Xbox360Control.LeftY => LeftY,
        Xbox360Control.RightX => RightX,
        Xbox360Control.RightY => RightY,
        _ => 0,
    };

    public void SetAxis(Xbox360Control control, short value)
    {
        switch (control)
        {
            case Xbox360Control.LeftX: LeftX = value; break;
            case Xbox360Control.LeftY: LeftY = value; break;
            case Xbox360Control.RightX: RightX = value; break;
            case Xbox360Control.RightY: RightY = value; break;
        }
    }

    public byte GetTrigger(Xbox360Control control) => control switch
    {
        Xbox360Control.LeftTrigger => LeftTrigger,
        Xbox360Control.RightTrigger => RightTrigger,
        _ => 0,
    };

    public void SetTrigger(Xbox360Control control, byte value)
    {
        switch (control)
        {
            case Xbox360Control.LeftTrigger: LeftTrigger = value; break;
            case Xbox360Control.RightTrigger: RightTrigger = value; break;
        }
    }

    public bool IsControlActive(Xbox360Control control)
    {
        if (control.IsButton())
        {
            return GetButton(control);
        }

        if (control.IsTrigger())
        {
            return GetTrigger(control) > 8;
        }

        return Math.Abs((int)GetAxis(control)) > 2000;
    }

    public void WritePacked(Span<byte> dest)
    {
        if (dest.Length < PackedSize)
        {
            throw new ArgumentException("Buffer too small.", nameof(dest));
        }

        BinaryPrimitives.WriteUInt16LittleEndian(dest, Buttons);
        dest[2] = LeftTrigger;
        dest[3] = RightTrigger;
        BinaryPrimitives.WriteInt16LittleEndian(dest[4..], LeftX);
        BinaryPrimitives.WriteInt16LittleEndian(dest[6..], LeftY);
        BinaryPrimitives.WriteInt16LittleEndian(dest[8..], RightX);
        BinaryPrimitives.WriteInt16LittleEndian(dest[10..], RightY);
    }

    public static Xbox360State ReadPacked(ReadOnlySpan<byte> src)
    {
        if (src.Length < PackedSize)
        {
            throw new ArgumentException("Buffer too small.", nameof(src));
        }

        return new Xbox360State
        {
            Buttons = BinaryPrimitives.ReadUInt16LittleEndian(src),
            LeftTrigger = src[2],
            RightTrigger = src[3],
            LeftX = BinaryPrimitives.ReadInt16LittleEndian(src[4..]),
            LeftY = BinaryPrimitives.ReadInt16LittleEndian(src[6..]),
            RightX = BinaryPrimitives.ReadInt16LittleEndian(src[8..]),
            RightY = BinaryPrimitives.ReadInt16LittleEndian(src[10..]),
        };
    }

    public bool Equals(Xbox360State other) =>
        Buttons == other.Buttons
        && LeftTrigger == other.LeftTrigger
        && RightTrigger == other.RightTrigger
        && LeftX == other.LeftX
        && LeftY == other.LeftY
        && RightX == other.RightX
        && RightY == other.RightY;

    public override bool Equals(object? obj) => obj is Xbox360State other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Buttons, LeftTrigger, RightTrigger, LeftX, LeftY, RightX, RightY);

    public static bool operator ==(Xbox360State left, Xbox360State right) => left.Equals(right);
    public static bool operator !=(Xbox360State left, Xbox360State right) => !left.Equals(right);
}
