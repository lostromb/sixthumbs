using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Sixthumbs.Core;

public static class Protocol
{
    public const uint Magic = 0x31485453; // "STH1" little-endian
    public const byte Version = 1;
    public const int MaxFrameSize = 1024;
    public const byte FrameTypeState = 1;
    public const int HandshakeSize = 4 + 1 + 32 + 2; // magic + ver + hash + nameLen, plus name
    public const int StatePayloadSize = 1 + 4 + Xbox360State.PackedSize;

    public static byte[] HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new byte[32];
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(password));
    }

    public static bool PasswordMatches(string hostPassword, ReadOnlySpan<byte> clientHash)
    {
        if (string.IsNullOrEmpty(hostPassword))
        {
            return true;
        }

        return CryptographicOperations.FixedTimeEquals(HashPassword(hostPassword), clientHash);
    }

    public static byte[] WriteHandshake(string password, string displayName)
    {
        displayName ??= "";
        var nameBytes = Encoding.UTF8.GetBytes(displayName);
        if (nameBytes.Length > 256)
        {
            throw new InvalidOperationException("Display name too long.");
        }

        var buffer = new byte[4 + 1 + 32 + 2 + nameBytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
        buffer[4] = Version;
        HashPassword(password).CopyTo(buffer, 5);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(37), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buffer.AsSpan(39));
        return buffer;
    }

    public static bool TryReadHandshake(ReadOnlySpan<byte> data, out byte[] passwordHash, out string displayName, out string? error)
    {
        passwordHash = [];
        displayName = "";
        error = null;
        if (data.Length < 39)
        {
            error = "Handshake truncated.";
            return false;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (magic != Magic)
        {
            error = "Bad magic.";
            return false;
        }

        if (data[4] != Version)
        {
            error = $"Unsupported protocol version {data[4]}.";
            return false;
        }

        passwordHash = data.Slice(5, 32).ToArray();
        var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(data[37..]);
        if (data.Length < 39 + nameLen)
        {
            error = "Handshake name truncated.";
            return false;
        }

        displayName = Encoding.UTF8.GetString(data.Slice(39, nameLen));
        return true;
    }

    public static byte[] WriteFrame(byte type, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxFrameSize)
        {
            throw new InvalidOperationException("Frame too large.");
        }

        var buffer = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)payload.Length);
        payload.CopyTo(buffer.AsSpan(4));
        _ = type;
        return buffer;
    }

    public static byte[] WriteStateFrame(uint sequence, Xbox360State state)
    {
        Span<byte> payload = stackalloc byte[StatePayloadSize];
        payload[0] = FrameTypeState;
        BinaryPrimitives.WriteUInt32LittleEndian(payload[1..], sequence);
        state.WritePacked(payload[5..]);
        return WriteFrame(FrameTypeState, payload);
    }

    public static bool TryReadStatePayload(ReadOnlySpan<byte> payload, out uint sequence, out Xbox360State state)
    {
        sequence = 0;
        state = default;
        if (payload.Length < StatePayloadSize || payload[0] != FrameTypeState)
        {
            return false;
        }

        sequence = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]);
        state = Xbox360State.ReadPacked(payload[5..]);
        return true;
    }
}
