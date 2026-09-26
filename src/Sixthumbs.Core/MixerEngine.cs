namespace Sixthumbs.Core;

public sealed class MixerEngine : IDisposable
{
    private readonly Func<IReadOnlyList<IInputSource>> _sources;
    private readonly Func<string, SourceMapping> _mappingFor;
    private readonly Action _pump;
    private readonly int _intervalMs;
    private IOutputSink _sink;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly object _gate = new();

    public MixerEngine(
        Func<IReadOnlyList<IInputSource>> sources,
        Func<string, SourceMapping> mappingFor,
        Action? pump = null,
        IOutputSink? sink = null,
        int intervalMs = 8)
    {
        _sources = sources;
        _mappingFor = mappingFor;
        _pump = pump ?? (() => { });
        _sink = sink ?? new NullOutputSink();
        _intervalMs = Math.Max(4, intervalMs);
    }

    public Xbox360State LastMerged { get; private set; }
    public IReadOnlyDictionary<string, Xbox360State> LastRaw { get; private set; } =
        new Dictionary<string, Xbox360State>();

    public event Action? Updated;

    public void SetSink(IOutputSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        IOutputSink old;
        lock (_gate)
        {
            old = _sink;
            _sink = sink;
        }

        if (!ReferenceEquals(old, sink))
        {
            old.Dispose();
        }
    }

    public void Start()
    {
        if (_loop != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Factory.StartNew(
            () => Run(_cts.Token),
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    private void Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                Tick();
            }
            catch
            {
                // Keep the mixer alive; UI surfaces sink/provider errors separately.
            }

            try
            {
                Task.Delay(_intervalMs, token).Wait(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Tick()
    {
        _pump();
        var sources = _sources();
        var mapped = new List<Xbox360State>(sources.Count);
        var raw = new Dictionary<string, Xbox360State>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!source.TryRead(out var state))
            {
                state = default;
            }

            raw[source.Id] = state;
            mapped.Add(InputMapper.Apply(state, _mappingFor(source.Id)));
        }

        var merged = InputMerger.Merge(mapped);
        LastMerged = merged;
        LastRaw = raw;
        IOutputSink sink;
        lock (_gate)
        {
            sink = _sink;
        }

        sink.Submit(merged);
        Updated?.Invoke();
    }

    public void Dispose()
    {
        Stop();
        lock (_gate)
        {
            _sink.Dispose();
            _sink = new NullOutputSink();
        }
    }
}
