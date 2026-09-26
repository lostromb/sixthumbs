namespace Sixthumbs.Core;

public static class EmulatedPadFilter
{
    public static bool MatchesNameOrPath(string? name, string? path)
    {
        return ContainsEmulatorMarker(name) || ContainsEmulatorMarker(path);
    }

    public static bool MatchesVirtualUserIndex(int playerIndex, int? virtualUserIndex) =>
        virtualUserIndex is >= 0 && playerIndex >= 0 && playerIndex == virtualUserIndex;

    public static bool ContainsEmulatorMarker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("ViGEm", StringComparison.OrdinalIgnoreCase)
               || value.Contains("Nefarius", StringComparison.OrdinalIgnoreCase)
               || value.Contains("Virtual Xbox", StringComparison.OrdinalIgnoreCase)
               || value.Contains("Virtual Gamepad", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldIgnore(
        string? name,
        string? path,
        int playerIndex,
        int? virtualUserIndex,
        IEnumerable<string>? deviceIdAncestry = null)
    {
        if (MatchesNameOrPath(name, path) || MatchesVirtualUserIndex(playerIndex, virtualUserIndex))
        {
            return true;
        }

        if (deviceIdAncestry is null)
        {
            return false;
        }

        foreach (var id in deviceIdAncestry)
        {
            if (ContainsEmulatorMarker(id))
            {
                return true;
            }
        }

        return false;
    }

    public static string? ToDeviceInstanceId(string? devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
        {
            return null;
        }

        var value = devicePath.Trim();
        if (value.StartsWith(@"\\?\", StringComparison.Ordinal) || value.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            value = value[4..];
        }

        var guid = value.IndexOf("#{", StringComparison.Ordinal);
        if (guid >= 0)
        {
            value = value[..guid];
        }

        return value.Replace('#', '\\');
    }
}
