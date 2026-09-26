using Sixthumbs.Core;

namespace Sixthumbs.Core.Tests;

public class EmulatedPadFilterTests
{
    [Theory]
    [InlineData("Virtual Xbox 360 Controller", null)]
    [InlineData("Xbox 360 Controller", @"\\?\USB#VID_045E&PID_028E#7&vigem#{guid}")]
    [InlineData(null, @"\\?\ROOT#SYSTEM#0000#{96e42b22-f5e9-42f8-b043-ed0f932f014f}\ViGEmBus")]
    public void Detects_vigem_name_or_path(string? name, string? path)
    {
        Assert.True(EmulatedPadFilter.ShouldIgnore(name, path, playerIndex: -1, virtualUserIndex: null));
    }

    [Fact]
    public void Detects_matching_xinput_slot()
    {
        Assert.True(EmulatedPadFilter.ShouldIgnore("Xbox 360 Controller", null, playerIndex: 0, virtualUserIndex: 0));
        Assert.False(EmulatedPadFilter.ShouldIgnore("Xbox 360 Controller", null, playerIndex: 1, virtualUserIndex: 0));
        Assert.False(EmulatedPadFilter.ShouldIgnore("Xbox 360 Controller", null, playerIndex: -1, virtualUserIndex: 0));
    }

    [Fact]
    public void Detects_parent_device_id()
    {
        var ancestry = new[]
        {
            @"USB\VID_045E&PID_028E\7&123",
            @"Nefarius\ViGEmBus\Gen1",
        };

        Assert.True(EmulatedPadFilter.ShouldIgnore("Xbox 360 Controller", null, -1, null, ancestry));
    }

    [Fact]
    public void Allows_ordinary_physical_pad()
    {
        Assert.False(EmulatedPadFilter.ShouldIgnore(
            "Xbox Series Controller",
            @"\\?\HID#VID_045E&PID_0B13#...",
            playerIndex: 0,
            virtualUserIndex: 1));
    }

    [Fact]
    public void Converts_hid_interface_path_to_instance_id()
    {
        var path = @"\\?\HID#VID_045E&PID_028E&IG_00#8&2c203035&2&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        Assert.Equal(@"HID\VID_045E&PID_028E&IG_00\8&2c203035&2&0000", EmulatedPadFilter.ToDeviceInstanceId(path));
    }
}
