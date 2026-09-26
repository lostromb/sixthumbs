namespace Sixthumbs.Core;

public interface IInputSource
{
    string Id { get; }
    string DisplayName { get; }
    bool TryRead(out Xbox360State state);
}

public interface IDescribedInputSource : IInputSource
{
    string Kind { get; }
    bool IsRemote { get; }
    int AxisCount { get; }
    int ButtonCount { get; }
    int HatCount { get; }
}

public interface IOutputSink : IDisposable
{
    void Submit(Xbox360State merged);
}

public interface IPhysicalInputProvider : IDisposable
{
    IReadOnlyList<IInputSource> Devices { get; }
    event Action? Changed;
    void Pump();
}

public sealed class NullOutputSink : IOutputSink
{
    public void Submit(Xbox360State merged)
    {
    }

    public void Dispose()
    {
    }
}

public sealed class SnapshotInputSource : IInputSource
{
    private Xbox360State _state;

    public SnapshotInputSource(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }

    public void Update(Xbox360State state) => _state = state;

    public bool TryRead(out Xbox360State state)
    {
        state = _state;
        return true;
    }
}
