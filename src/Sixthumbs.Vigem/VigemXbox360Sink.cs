using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Sixthumbs.Core;

namespace Sixthumbs.Vigem;

public sealed class VigemXbox360Sink : IOutputSink
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _pad;
    private int? _userIndex;

    public VigemXbox360Sink()
    {
        _client = new ViGEmClient();
        _pad = _client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
        _pad.Connect();
        TryReadUserIndex();
    }

    public int? UserIndex
    {
        get
        {
            TryReadUserIndex();
            return _userIndex;
        }
    }

    public void Submit(Xbox360State merged)
    {
        _pad.SetButtonsFull(merged.Buttons);
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, merged.LeftTrigger);
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, merged.RightTrigger);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, merged.LeftX);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, InvertY(merged.LeftY));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, merged.RightX);
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, InvertY(merged.RightY));
        _pad.SubmitReport();
        TryReadUserIndex();
    }

    // Internal stick Y matches the on-screen preview (positive down). XInput is opposite.
    private static short InvertY(short value) =>
        value == short.MinValue ? short.MaxValue : (short)-value;

    private void TryReadUserIndex()
    {
        if (_userIndex is not null)
        {
            return;
        }

        try
        {
            _userIndex = _pad.UserIndex;
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        try
        {
            _pad.Disconnect();
        }
        catch
        {
        }

        _client.Dispose();
    }
}
