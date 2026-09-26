using Sixthumbs.Core;
using static SDL.SDL3;
using SDL;

namespace Sixthumbs.Sdl;

public sealed unsafe class SdlInputProvider : IPhysicalInputProvider
{
    private readonly Func<string, SourceMapping> _mappingFor;
    private readonly Dictionary<uint, SdlDevice> _devices = [];
    private readonly HashSet<uint> _ignored = [];
    private readonly List<IInputSource> _snapshot = [];
    private bool _started;

    public Func<int?>? VirtualUserIndex { get; set; }

    public SdlInputProvider(Func<string, SourceMapping>? mappingFor = null)
    {
        _mappingFor = mappingFor ?? (_ => new SourceMapping());
    }

    public IReadOnlyList<IInputSource> Devices
    {
        get
        {
            lock (_devices)
            {
                return _snapshot.ToArray();
            }
        }
    }

    public event Action? Changed;

    public string? LastError { get; private set; }

    public bool Start()
    {
        if (_started)
        {
            return true;
        }

        SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1"u8);
        if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_GAMEPAD))
        {
            LastError = SDL_GetError();
            return false;
        }

        SDL_SetJoystickEventsEnabled(true);
        SDL_SetGamepadEventsEnabled(true);
        _started = true;
        Pump();
        return true;
    }

    public void IgnoreInstance(uint instanceId) => _ignored.Add(instanceId);

    private bool ShouldSkip(uint instanceId)
    {
        if (_ignored.Contains(instanceId))
        {
            return true;
        }

        var sdlId = (SDL_JoystickID)instanceId;
        if (SDL_IsJoystickVirtual(sdlId))
        {
            return true;
        }

        var name = SDL_GetJoystickNameForID(sdlId);
        var path = SDL_GetJoystickPathForID(sdlId);
        var playerIndex = SDL_GetJoystickPlayerIndexForID(sdlId);
        if (SDL_IsGamepad(sdlId))
        {
            var gamepadIndex = SDL_GetGamepadPlayerIndexForID(sdlId);
            if (gamepadIndex >= 0)
            {
                playerIndex = gamepadIndex;
            }
        }

        IReadOnlyList<string>? ancestry = null;
        if (OperatingSystem.IsWindows())
        {
            ancestry = WindowsDeviceAncestry.Walk(path);
        }

        return EmulatedPadFilter.ShouldIgnore(name, path, playerIndex, VirtualUserIndex?.Invoke(), ancestry);
    }

    public void Pump()
    {
        if (!_started)
        {
            return;
        }

        SDL_PumpEvents();
        SDL_UpdateJoysticks();
        SDL_UpdateGamepads();
        SyncDevices();
        foreach (var device in _devices.Values)
        {
            device.Poll(_mappingFor);
        }
    }

    private void SyncDevices()
    {
        var present = new HashSet<uint>(EnumerateJoystickIds());
        var changed = false;

        foreach (var id in present)
        {
            if (ShouldSkip(id))
            {
                _ignored.Add(id);
                if (_devices.Remove(id, out var skipped))
                {
                    skipped.Dispose();
                    changed = true;
                }

                continue;
            }

            if (_devices.ContainsKey(id))
            {
                continue;
            }

            var device = SdlDevice.Open(id);
            if (device is null)
            {
                continue;
            }

            _devices[id] = device;
            changed = true;
        }

        foreach (var id in _devices.Keys.ToArray())
        {
            if (present.Contains(id))
            {
                continue;
            }

            _devices[id].Dispose();
            _devices.Remove(id);
            changed = true;
        }

        if (changed)
        {
            lock (_devices)
            {
                _snapshot.Clear();
                _snapshot.AddRange(_devices.Values.OrderBy(d => d.DisplayName));
            }

            Changed?.Invoke();
        }
    }

    private static List<uint> EnumerateJoystickIds()
    {
        var result = new List<uint>();
        using var joysticks = SDL_GetJoysticks();
        if (joysticks is null)
        {
            return result;
        }

        for (var i = 0; i < joysticks.Count; i++)
        {
            result.Add((uint)joysticks[i]);
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var device in _devices.Values)
        {
            device.Dispose();
        }

        _devices.Clear();
        if (_started)
        {
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_GAMEPAD);
            _started = false;
        }
    }

    private sealed class SdlDevice : IDescribedInputSource, IDisposable
    {
        private readonly uint _instanceId;
        private readonly SDL_Gamepad* _gamepad;
        private readonly SDL_Joystick* _joystick;
        private Xbox360State _state;

        private SdlDevice(uint instanceId, string id, string displayName, bool isGamepad, SDL_Gamepad* gamepad, SDL_Joystick* joystick, int axes, int buttons, int hats)
        {
            _instanceId = instanceId;
            Id = id;
            DisplayName = displayName;
            Kind = isGamepad ? "gamepad" : "joystick";
            _gamepad = gamepad;
            _joystick = joystick;
            AxisCount = axes;
            ButtonCount = buttons;
            HatCount = hats;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Kind { get; }
        public bool IsRemote => false;
        public int AxisCount { get; }
        public int ButtonCount { get; }
        public int HatCount { get; }

        public static SdlDevice? Open(uint instanceId)
        {
            var sdlId = (SDL_JoystickID)instanceId;
            var guid = GuidString(SDL_GetJoystickGUIDForID(sdlId));
            var isGamepad = SDL_IsGamepad(sdlId);
            string name;
            SDL_Gamepad* gamepad = null;
            SDL_Joystick* joystick;
            int axes, buttons, hats;

            if (isGamepad)
            {
                gamepad = SDL_OpenGamepad(sdlId);
                if (gamepad is null)
                {
                    return null;
                }

                joystick = SDL_GetGamepadJoystick(gamepad);
                name = SDL_GetGamepadName(gamepad) ?? "Gamepad";
            }
            else
            {
                joystick = SDL_OpenJoystick(sdlId);
                if (joystick is null)
                {
                    return null;
                }

                name = SDL_GetJoystickName(joystick) ?? "Joystick";
            }

            axes = SDL_GetNumJoystickAxes(joystick);
            buttons = SDL_GetNumJoystickButtons(joystick);
            hats = SDL_GetNumJoystickHats(joystick);
            var id = $"sdl:{guid}:{name}";
            return new SdlDevice(instanceId, id, name, isGamepad, gamepad, joystick, axes, buttons, hats);
        }

        public void Poll(Func<string, SourceMapping> mappingFor)
        {
            if (_gamepad is not null)
            {
                _state = ReadGamepad(_gamepad);
                return;
            }

            var mapping = mappingFor(Id);
            mapping.EnsureRawJoystickLayout(AxisCount, ButtonCount, HatCount);
            _state = ReadJoystick(_joystick, mapping.Joystick!);
        }

        public bool TryRead(out Xbox360State state)
        {
            state = _state;
            return true;
        }

        public void Dispose()
        {
            if (_gamepad is not null)
            {
                SDL_CloseGamepad(_gamepad);
            }
            else if (_joystick is not null)
            {
                SDL_CloseJoystick(_joystick);
            }
        }

        private static Xbox360State ReadGamepad(SDL_Gamepad* pad)
        {
            var state = new Xbox360State();
            Set(ref state, Xbox360Control.A, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH));
            Set(ref state, Xbox360Control.B, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST));
            Set(ref state, Xbox360Control.X, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST));
            Set(ref state, Xbox360Control.Y, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH));
            Set(ref state, Xbox360Control.Back, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK));
            Set(ref state, Xbox360Control.Guide, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE));
            Set(ref state, Xbox360Control.Start, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START));
            Set(ref state, Xbox360Control.LeftStick, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK));
            Set(ref state, Xbox360Control.RightStick, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK));
            Set(ref state, Xbox360Control.LeftShoulder, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER));
            Set(ref state, Xbox360Control.RightShoulder, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER));
            Set(ref state, Xbox360Control.DpadUp, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP));
            Set(ref state, Xbox360Control.DpadDown, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN));
            Set(ref state, Xbox360Control.DpadLeft, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT));
            Set(ref state, Xbox360Control.DpadRight, SDL_GetGamepadButton(pad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT));
            state.LeftX = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX);
            state.LeftY = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY);
            state.RightX = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX);
            state.RightY = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY);
            state.LeftTrigger = AxisToTrigger(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER));
            state.RightTrigger = AxisToTrigger(SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER));
            return state;
        }

        private static Xbox360State ReadJoystick(SDL_Joystick* joystick, JoystickLayout layout)
        {
            var axes = new short[Math.Max(0, SDL_GetNumJoystickAxes(joystick))];
            for (var i = 0; i < axes.Length; i++)
            {
                axes[i] = SDL_GetJoystickAxis(joystick, i);
            }

            var buttons = new bool[Math.Max(0, SDL_GetNumJoystickButtons(joystick))];
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i] = SDL_GetJoystickButton(joystick, i);
            }

            var hats = new Xbox360Buttons[Math.Max(0, SDL_GetNumJoystickHats(joystick))];
            for (var i = 0; i < hats.Length; i++)
            {
                hats[i] = HatToDpad(SDL_GetJoystickHat(joystick, i));
            }

            return InputMapper.FromJoystick(axes, buttons, hats, layout);
        }

        private static void Set(ref Xbox360State state, Xbox360Control control, bool pressed) =>
            state.SetButton(control, pressed);

        private static byte AxisToTrigger(short axis)
        {
            if (axis < 0)
            {
                axis = 0;
            }

            return (byte)Math.Clamp(axis * 255 / 32767, 0, 255);
        }

        private static Xbox360Buttons HatToDpad(byte hat)
        {
            var buttons = Xbox360Buttons.None;
            if ((hat & SDL_HAT_UP) != 0)
            {
                buttons |= Xbox360Buttons.DpadUp;
            }

            if ((hat & SDL_HAT_DOWN) != 0)
            {
                buttons |= Xbox360Buttons.DpadDown;
            }

            if ((hat & SDL_HAT_LEFT) != 0)
            {
                buttons |= Xbox360Buttons.DpadLeft;
            }

            if ((hat & SDL_HAT_RIGHT) != 0)
            {
                buttons |= Xbox360Buttons.DpadRight;
            }

            return buttons;
        }

        private static string GuidString(SDL_GUID guid) => Convert.ToHexString(GetGuidBytes(guid));

        private static byte[] GetGuidBytes(SDL_GUID guid)
        {
            var bytes = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                bytes[i] = guid.data[i];
            }

            return bytes;
        }
    }
}
