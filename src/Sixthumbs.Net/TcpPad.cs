using System.Buffers.Binary;
using System.Net.Sockets;
using Sixthumbs.Core;

namespace Sixthumbs.Net;

public static class FrameIo
{
    public static async Task WriteAllAsync(Stream stream, byte[] data, CancellationToken token)
    {
        await stream.WriteAsync(data, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    public static async Task<byte[]?> ReadExactAsync(Stream stream, int count, CancellationToken token)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, count - read), token).ConfigureAwait(false);
            if (n == 0)
            {
                return null;
            }

            read += n;
        }

        return buffer;
    }

    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken token)
    {
        var header = await ReadExactAsync(stream, 4, token).ConfigureAwait(false);
        if (header is null)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length == 0 || length > Protocol.MaxFrameSize)
        {
            throw new InvalidDataException($"Invalid frame length {length}.");
        }

        return await ReadExactAsync(stream, (int)length, token).ConfigureAwait(false);
    }

    public static async Task<(bool ok, string? error, string displayName, byte[] passwordHash)> ReadHandshakeAsync(
        Stream stream,
        CancellationToken token)
    {
        var header = await ReadExactAsync(stream, 39, token).ConfigureAwait(false);
        if (header is null)
        {
            return (false, "Disconnected during handshake.", "", []);
        }

        var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(37));
        if (nameLen > 256)
        {
            return (false, "Name too long.", "", []);
        }

        var nameBytes = nameLen == 0 ? [] : await ReadExactAsync(stream, nameLen, token).ConfigureAwait(false);
        if (nameLen > 0 && nameBytes is null)
        {
            return (false, "Disconnected during handshake name.", "", []);
        }

        var full = new byte[39 + nameLen];
        header.CopyTo(full, 0);
        if (nameBytes is not null && nameLen > 0)
        {
            nameBytes.CopyTo(full, 39);
        }

        if (!Protocol.TryReadHandshake(full, out var hash, out var name, out var error))
        {
            return (false, error, "", []);
        }

        return (true, null, name, hash);
    }

    public static async Task WriteHandshakeResultAsync(Stream stream, bool ok, string? error, CancellationToken token)
    {
        error ??= "";
        var message = System.Text.Encoding.UTF8.GetBytes(error);
        var buffer = new byte[1 + 2 + message.Length];
        buffer[0] = (byte)(ok ? 1 : 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1), (ushort)message.Length);
        message.CopyTo(buffer, 3);
        await WriteAllAsync(stream, buffer, token).ConfigureAwait(false);
    }

    public static async Task<(bool ok, string? error)> ReadHandshakeResultAsync(Stream stream, CancellationToken token)
    {
        var header = await ReadExactAsync(stream, 3, token).ConfigureAwait(false);
        if (header is null)
        {
            return (false, "Disconnected during handshake reply.");
        }

        var ok = header[0] != 0;
        var len = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(1));
        var messageBytes = len == 0 ? [] : await ReadExactAsync(stream, len, token).ConfigureAwait(false);
        var message = messageBytes is null || messageBytes.Length == 0
            ? null
            : System.Text.Encoding.UTF8.GetString(messageBytes);
        return (ok, ok ? null : message ?? "Handshake rejected.");
    }
}

public sealed class TcpRemoteSource : IDescribedInputSource
{
    private readonly object _gate = new();
    private Xbox360State _state;
    private DateTime _lastUtc = DateTime.UtcNow;

    public TcpRemoteSource(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Kind => "network";
    public bool IsRemote => true;
    public int AxisCount => 0;
    public int ButtonCount => 0;
    public int HatCount => 0;
    public DateTime LastUtc
    {
        get
        {
            lock (_gate)
            {
                return _lastUtc;
            }
        }
    }

    public void Update(Xbox360State state)
    {
        lock (_gate)
        {
            _state = state;
            _lastUtc = DateTime.UtcNow;
        }
    }

    public void Zero()
    {
        lock (_gate)
        {
            _state = default;
        }
    }

    public bool TryRead(out Xbox360State state)
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _lastUtc > TimeSpan.FromMilliseconds(500))
            {
                state = default;
                return true;
            }

            state = _state;
            return true;
        }
    }
}

public sealed class TcpPadHost : IDisposable
{
    private readonly Dictionary<string, TcpRemoteSource> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _nextId;

    public event Action? Changed;
    public string? LastError { get; private set; }
    public int PeerCount
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    public IReadOnlyList<IInputSource> Sessions
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Values.Cast<IInputSource>().ToArray();
            }
        }
    }

    public void Start(int port, string password)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(System.Net.IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoop(password, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        _listener = null;
        lock (_gate)
        {
            _sessions.Clear();
        }

        Changed?.Invoke();
    }

    private async Task AcceptLoop(string password, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _listener is not null)
            {
                var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = HandleClient(client, password, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private async Task HandleClient(TcpClient client, string password, CancellationToken token)
    {
        TcpRemoteSource? source = null;
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            {
                var handshake = await FrameIo.ReadHandshakeAsync(stream, token).ConfigureAwait(false);
                if (!handshake.ok)
                {
                    await FrameIo.WriteHandshakeResultAsync(stream, false, handshake.error, token).ConfigureAwait(false);
                    return;
                }

                if (!Protocol.PasswordMatches(password, handshake.passwordHash))
                {
                    await FrameIo.WriteHandshakeResultAsync(stream, false, "Bad password.", token).ConfigureAwait(false);
                    return;
                }

                await FrameIo.WriteHandshakeResultAsync(stream, true, null, token).ConfigureAwait(false);
                var id = $"tcp:{Interlocked.Increment(ref _nextId)}:{handshake.displayName}";
                source = new TcpRemoteSource(id, string.IsNullOrWhiteSpace(handshake.displayName) ? "Remote pad" : handshake.displayName);
                lock (_gate)
                {
                    _sessions[id] = source;
                }

                Changed?.Invoke();

                while (!token.IsCancellationRequested)
                {
                    var payload = await FrameIo.ReadFrameAsync(stream, token).ConfigureAwait(false);
                    if (payload is null)
                    {
                        break;
                    }

                    if (Protocol.TryReadStatePayload(payload, out _, out var state))
                    {
                        source.Update(state);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            if (source is not null)
            {
                lock (_gate)
                {
                    _sessions.Remove(source.Id);
                }

                Changed?.Invoke();
            }
        }
    }

    public void Dispose() => Stop();
}

public sealed class TcpPadClientSink : IOutputSink
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private uint _sequence;

    private TcpPadClientSink(TcpClient client, NetworkStream stream)
    {
        _client = client;
        _stream = stream;
    }

    public static async Task<TcpPadClientSink> ConnectAsync(
        string host,
        int port,
        string password,
        string displayName,
        CancellationToken token)
    {
        var client = new TcpClient();
        var parts = host.Split(':', 2);
        var hostname = parts[0];
        if (parts.Length == 2 && int.TryParse(parts[1], out var parsedPort))
        {
            port = parsedPort;
        }

        await client.ConnectAsync(hostname, port, token).ConfigureAwait(false);
        var stream = client.GetStream();
        await FrameIo.WriteAllAsync(stream, Protocol.WriteHandshake(password, displayName), token).ConfigureAwait(false);
        var result = await FrameIo.ReadHandshakeResultAsync(stream, token).ConfigureAwait(false);
        if (!result.ok)
        {
            client.Dispose();
            throw new InvalidOperationException(result.error ?? "Handshake failed.");
        }

        return new TcpPadClientSink(client, stream);
    }

    public void Submit(Xbox360State merged)
    {
        var frame = Protocol.WriteStateFrame(++_sequence, merged);
        try
        {
            _stream.Write(frame);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
