using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Sixthumbs.Core;
using Sixthumbs.Net;
using Sixthumbs.Sdl;
using Sixthumbs.Vigem;

namespace Sixthumbs.App;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ConfigStore _config = new();
    private readonly DispatcherTimer _uiTimer;
    private MixerEngine? _engine;
    private SdlInputProvider? _sdl;
    private TcpPadHost? _tcpHost;
    private IOutputSink? _sink;

    public MainViewModel()
    {
        Settings = _config.Load();
        Role = Settings.Role;
        ListenPort = Settings.ListenPort;
        Password = Settings.Password;
        HostAddress = Settings.HostAddress;
        ListenEnabled = Settings.ListenEnabled;
        Controls = Xbox360Controls.All.Select(c => c).ToArray();
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _uiTimer.Tick += (_, _) => RefreshLive();
        _uiTimer.Start();
    }

    public AppSettings Settings { get; }
    public Xbox360Control[] Controls { get; }
    public IReadOnlyList<RawMappingTarget> RawDestinations { get; } = RawMappingTarget.All;
    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    [ObservableProperty] private AppRole _role;
    [ObservableProperty] private int _listenPort;
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _hostAddress = "127.0.0.1";
    [ObservableProperty] private bool _listenEnabled;
    [ObservableProperty] private bool _running;
    [ObservableProperty] private string _status = "Stopped";
    [ObservableProperty] private string _outputStatus = "Output is stopped";
    [ObservableProperty] private int? _userIndex;
    [ObservableProperty] private int _peerCount;
    [ObservableProperty] private Xbox360State _mergedState;
    [ObservableProperty] private DeviceViewModel? _selectedDevice;
    [ObservableProperty] private ObservableCollection<MappingRowViewModel> _mappingRows = [];
    [ObservableProperty] private ObservableCollection<JoystickBindRowViewModel> _joystickRows = [];
    [ObservableProperty] private bool _showJoystickBindings;

    public bool IsHost
    {
        get => Role == AppRole.Host;
        set
        {
            Role = value ? AppRole.Host : AppRole.Client;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsClient));
        }
    }

    public bool IsClient
    {
        get => Role == AppRole.Client;
        set
        {
            Role = value ? AppRole.Client : AppRole.Host;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsHost));
        }
    }

    partial void OnRoleChanged(AppRole value)
    {
        OnPropertyChanged(nameof(IsHost));
        OnPropertyChanged(nameof(IsClient));
    }

    partial void OnSelectedDeviceChanged(DeviceViewModel? value)
    {
        RebuildMappingEditor();
        SavePresetCommand.NotifyCanExecuteChanged();
        LoadPresetCommand.NotifyCanExecuteChanged();
        ResetDefaultsCommand.NotifyCanExecuteChanged();
    }

    private bool CanEditPreset() => SelectedDevice is not null;

    [RelayCommand(CanExecute = nameof(CanEditPreset))]
    private void ResetDefaults()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        Settings.MappingFor(SelectedDevice.Id).ResetToDefaults();
        PersistSettings();
        RebuildMappingEditor();
        Status = $"Reset mapping for {SelectedDevice.DisplayName}";
    }

    [RelayCommand(CanExecute = nameof(CanEditPreset))]
    private void SavePreset()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save mapping preset",
            Filter = "Sixthumbs preset (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "Preset 1.json",
            InitialDirectory = PresetDirectory(),
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            MappingPresetStore.SaveFile(dialog.FileName, Settings.MappingFor(SelectedDevice.Id));
            Status = $"Saved preset {System.IO.Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save preset failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditPreset))]
    private void LoadPreset()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Load mapping preset",
            Filter = "Sixthumbs preset (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            InitialDirectory = PresetDirectory(),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            MappingPresetStore.LoadFile(dialog.FileName, Settings.MappingFor(SelectedDevice.Id));
            PersistSettings();
            RebuildMappingEditor();
            Status = $"Loaded preset {System.IO.Path.GetFileName(dialog.FileName)} onto {SelectedDevice.DisplayName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load preset failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string PresetDirectory()
    {
        var directory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Sixthumbs",
            "Presets");
        Directory.CreateDirectory(directory);
        return directory;
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (Running)
        {
            Stop();
            return;
        }

        await StartAsync().ConfigureAwait(true);
    }

    private async Task StartAsync()
    {
        PersistSettings();
        _sdl = new SdlInputProvider(id => Settings.MappingFor(id));
        if (!_sdl.Start())
        {
            Status = $"SDL failed: {_sdl.LastError}";
            _sdl.Dispose();
            _sdl = null;
            return;
        }

        _sdl.Changed += OnDevicesChanged;
        OnDevicesChanged();

        IOutputSink sink;
        try
        {
            if (Role == AppRole.Host)
            {
                var vigem = new VigemXbox360Sink();
                sink = vigem;
                _sdl.VirtualUserIndex = () => vigem.UserIndex;
                UserIndex = vigem.UserIndex;
                _sdl.Pump();
                OnDevicesChanged();
                if (ListenEnabled)
                {
                    _tcpHost = new TcpPadHost();
                    _tcpHost.Changed += OnDevicesChanged;
                    _tcpHost.Start(ListenPort, Password);
                }
            }
            else
            {
                sink = await TcpPadClientSink.ConnectAsync(
                    HostAddress,
                    ListenPort,
                    Password,
                    Environment.MachineName,
                    CancellationToken.None).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            _sdl.Dispose();
            _sdl = null;
            return;
        }

        _sink = sink;
        _engine = new MixerEngine(CollectSources, Settings.MappingFor, () => _sdl?.Pump(), sink);
        _engine.Start();
        Running = true;
        Status = Role == AppRole.Host ? "Host running" : "Client connected";
        UpdateOutputStatus();
    }

    private IReadOnlyList<IInputSource> CollectSources()
    {
        var list = new List<IInputSource>();
        if (_sdl is not null)
        {
            list.AddRange(_sdl.Devices);
        }

        if (_tcpHost is not null)
        {
            list.AddRange(_tcpHost.Sessions);
        }

        return list;
    }

    private void OnDevicesChanged()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(RebuildDeviceList);
    }

    private void RebuildDeviceList()
    {
        var selectedId = SelectedDevice?.Id;
        Devices.Clear();
        foreach (var source in CollectSources())
        {
            var mapping = Settings.MappingFor(source.Id);
            var described = source as IDescribedInputSource;
            if (described?.Kind == "joystick")
            {
                mapping.EnsureRawJoystickLayout(
                    described.AxisCount,
                    described.ButtonCount,
                    described.HatCount);
            }

            Devices.Add(new DeviceViewModel(source.Id, source.DisplayName, described?.Kind ?? "pad", described?.IsRemote == true, mapping, RebuildMappingEditor)
            {
                AxisCount = described?.AxisCount ?? 0,
                ButtonCount = described?.ButtonCount ?? 0,
                HatCount = described?.HatCount ?? 0,
            });
        }

        SelectedDevice = Devices.FirstOrDefault(d => d.Id == selectedId) ?? Devices.FirstOrDefault();
        PeerCount = _tcpHost?.PeerCount ?? 0;
        PersistSettings();
    }

    private void RebuildMappingEditor()
    {
        MappingRows.Clear();
        JoystickRows.Clear();
        ShowJoystickBindings = false;
        if (SelectedDevice is null)
        {
            return;
        }

        var mapping = Settings.MappingFor(SelectedDevice.Id);
        foreach (var control in Xbox360Controls.All)
        {
            MappingRows.Add(new MappingRowViewModel(control, mapping.GetOrCreate(control), PersistSettings));
        }

        if (SelectedDevice.Kind == "joystick")
        {
            ShowJoystickBindings = true;
            var layout = mapping.EnsureRawJoystickLayout(
                SelectedDevice.AxisCount,
                SelectedDevice.ButtonCount,
                SelectedDevice.HatCount);
            foreach (var axis in layout.AxesForDevice(SelectedDevice.AxisCount))
            {
                JoystickRows.Add(JoystickBindRowViewModel.ForAxis(axis, PersistSettings));
            }

            foreach (var button in layout.ButtonsForDevice(SelectedDevice.ButtonCount))
            {
                JoystickRows.Add(JoystickBindRowViewModel.ForButton(button, PersistSettings));
            }
        }
    }

    private void RefreshLive()
    {
        if (_engine is null)
        {
            return;
        }

        MergedState = _engine.LastMerged;
        var raw = _engine.LastRaw;
        foreach (var device in Devices)
        {
            if (raw.TryGetValue(device.Id, out var state))
            {
                device.LiveState = state;
            }
        }

        if (SelectedDevice is not null)
        {
            var live = SelectedDevice.LiveState;
            foreach (var row in MappingRows)
            {
                row.IsLive = live.IsControlActive(row.Source);
            }

            IReadOnlyList<short>? axes = null;
            IReadOnlyList<bool>? buttons = null;
            foreach (var source in CollectSources())
            {
                if (source.Id == SelectedDevice.Id && source is IRawJoystickSource rawPad && rawPad.TryReadRaw(out axes, out buttons))
                {
                    break;
                }
            }

            foreach (var row in JoystickRows)
            {
                row.UpdateVisualizer(axes, buttons);
            }
        }

        UpdateOutputStatus();
        PeerCount = _tcpHost?.PeerCount ?? 0;
        if (_tcpHost?.LastError is { Length: > 0 } err)
        {
            Status = err;
        }
    }

    public void Stop()
    {
        _engine?.Dispose();
        _engine = null;
        _tcpHost?.Dispose();
        _tcpHost = null;
        _sink = null;
        _sdl?.Dispose();
        _sdl = null;
        Running = false;
        UserIndex = null;
        OutputStatus = "Output is stopped";
        Status = "Stopped";
        PersistSettings();
        Devices.Clear();
        MappingRows.Clear();
    }

    private void PersistSettings()
    {
        Settings.Role = Role;
        Settings.ListenPort = ListenPort;
        Settings.Password = Password;
        Settings.HostAddress = HostAddress;
        Settings.ListenEnabled = ListenEnabled;
        _config.Save(Settings);
    }

    private void UpdateOutputStatus()
    {
        if (_sink is TcpPadClientSink)
        {
            OutputStatus = "Output controller is going to network host";
            return;
        }

        if (_sink is VigemXbox360Sink vigem)
        {
            UserIndex = vigem.UserIndex;
            OutputStatus = UserIndex is { } index
                ? $"Output controller is player {index + 1}"
                : "Output controller player slot is not assigned yet";
            return;
        }

        OutputStatus = Running ? "Output controller is not connected" : "Output is stopped";
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        Stop();
    }
}

public sealed partial class DeviceViewModel : ObservableObject
{
    private readonly SourceMapping _mapping;
    private readonly Action _changed;

    public DeviceViewModel(string id, string displayName, string kind, bool isRemote, SourceMapping mapping, Action changed)
    {
        Id = id;
        DisplayName = displayName;
        Kind = kind;
        IsRemote = isRemote;
        _mapping = mapping;
        _changed = changed;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Kind { get; }
    public bool IsRemote { get; }
    public int AxisCount { get; set; }
    public int ButtonCount { get; set; }
    public int HatCount { get; set; }

    public bool Muted
    {
        get => _mapping.Muted;
        set
        {
            _mapping.Muted = value;
            OnPropertyChanged();
            _changed();
        }
    }

    [ObservableProperty] private Xbox360State _liveState;
}

public sealed partial class MappingRowViewModel : ObservableObject
{
    private readonly ControlBinding _binding;
    private readonly Action _changed;

    public MappingRowViewModel(Xbox360Control destination, ControlBinding binding, Action changed)
    {
        Destination = destination;
        _binding = binding;
        _changed = changed;
    }

    public Xbox360Control Destination { get; }
    public string DestinationName => Destination.DisplayName();

    public bool Enabled
    {
        get => _binding.Enabled;
        set
        {
            _binding.Enabled = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public Xbox360Control Source
    {
        get => _binding.Source;
        set
        {
            _binding.Source = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public bool Invert
    {
        get => _binding.Invert;
        set
        {
            _binding.Invert = value;
            OnPropertyChanged();
            _changed();
        }
    }

    public double Deadzone
    {
        get => _binding.Deadzone;
        set
        {
            _binding.Deadzone = (float)value;
            OnPropertyChanged();
            _changed();
        }
    }

    public bool ShowAnalogOptions => !Destination.IsButton();

    [ObservableProperty] private bool _isLive;
}

public sealed partial class JoystickBindRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly JoystickAxisBinding? _axis;
    private readonly JoystickButtonBinding? _button;

    private JoystickBindRowViewModel(string label, Action changed, JoystickAxisBinding? axis, JoystickButtonBinding? button)
    {
        Label = label;
        _changed = changed;
        _axis = axis;
        _button = button;
    }

    public static JoystickBindRowViewModel ForAxis(JoystickAxisBinding axis, Action changed) =>
        new($"Axis {axis.AxisIndex}", changed, axis, null);

    public static JoystickBindRowViewModel ForButton(JoystickButtonBinding button, Action changed) =>
        new($"Button {button.ButtonIndex}", changed, null, button);

    public string Label { get; }

    public RawMappingTarget Destination
    {
        get => RawMappingTarget.From(_axis?.Destination ?? _button?.Destination);
        set
        {
            var dest = value?.Control;
            if (_axis is not null)
            {
                _axis.Destination = dest;
            }
            else
            {
                _button!.Destination = dest;
            }

            OnPropertyChanged();
            _changed();
        }
    }

    public bool Invert
    {
        get => _axis?.Invert ?? false;
        set
        {
            if (_axis is not null)
            {
                _axis.Invert = value;
                OnPropertyChanged();
                _changed();
            }
        }
    }

    public bool ShowInvert => _axis is not null;

    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private double _visualizer;

    public void UpdateVisualizer(IReadOnlyList<short>? axes, IReadOnlyList<bool>? buttons)
    {
        if (_axis is not null)
        {
            var value = axes is not null && _axis.AxisIndex >= 0 && _axis.AxisIndex < axes.Count
                ? axes[_axis.AxisIndex]
                : (short)0;
            Visualizer = (value + 32768.0) / 65535.0;
            IsLive = Math.Abs((int)value) > 2000;
            return;
        }

        var pressed = buttons is not null && _button is not null
                      && _button.ButtonIndex >= 0 && _button.ButtonIndex < buttons.Count
                      && buttons[_button.ButtonIndex];
        Visualizer = pressed ? 1 : 0;
        IsLive = pressed;
    }
}
