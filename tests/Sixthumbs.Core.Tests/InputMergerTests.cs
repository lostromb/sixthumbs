using Sixthumbs.Core;

namespace Sixthumbs.Core.Tests;

public class InputMergerTests
{
    [Fact]
    public void Buttons_are_or_merged()
    {
        var a = new Xbox360State();
        a.SetButton(Xbox360Control.A, true);
        var b = new Xbox360State();
        b.SetButton(Xbox360Control.B, true);

        var merged = InputMerger.Merge([a, b]);

        Assert.True(merged.GetButton(Xbox360Control.A));
        Assert.True(merged.GetButton(Xbox360Control.B));
        Assert.False(merged.GetButton(Xbox360Control.X));
    }

    [Fact]
    public void Analog_takes_largest_magnitude()
    {
        var a = new Xbox360State { LeftX = 1000 };
        var b = new Xbox360State { LeftX = -4000 };

        var merged = InputMerger.Merge([a, b]);

        Assert.Equal(-4000, merged.LeftX);
    }

    [Fact]
    public void Analog_tie_keeps_current()
    {
        var a = new Xbox360State { LeftY = 2000 };
        var b = new Xbox360State { LeftY = -2000 };

        var merged = InputMerger.Merge([a, b]);

        Assert.Equal(2000, merged.LeftY);
    }

    [Fact]
    public void Triggers_take_max()
    {
        var a = new Xbox360State { LeftTrigger = 10, RightTrigger = 200 };
        var b = new Xbox360State { LeftTrigger = 40, RightTrigger = 50 };

        var merged = InputMerger.Merge([a, b]);

        Assert.Equal(40, merged.LeftTrigger);
        Assert.Equal(200, merged.RightTrigger);
    }

    [Fact]
    public void Idle_zeros_lose()
    {
        var a = new Xbox360State();
        var b = new Xbox360State { RightX = 12345 };
        b.SetButton(Xbox360Control.Start, true);

        var merged = InputMerger.Merge([a, b]);

        Assert.Equal(12345, merged.RightX);
        Assert.True(merged.GetButton(Xbox360Control.Start));
    }
}
