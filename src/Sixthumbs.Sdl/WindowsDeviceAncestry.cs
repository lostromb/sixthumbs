using System.Runtime.InteropServices;
using System.Text;
using Sixthumbs.Core;

namespace Sixthumbs.Sdl;

internal static class WindowsDeviceAncestry
{
    private const int CrSuccess = 0;
    private const int MaxDepth = 12;

    public static IReadOnlyList<string> Walk(string? devicePath)
    {
        var instanceId = EmulatedPadFilter.ToDeviceInstanceId(devicePath);
        if (instanceId is null)
        {
            return [];
        }

        var ids = new List<string>();
        if (CM_Locate_DevNode(out var node, instanceId, 0) != CrSuccess)
        {
            return ids;
        }

        for (var i = 0; i < MaxDepth; i++)
        {
            var id = GetDeviceId(node);
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }

            if (CM_Get_Parent(out var parent, node, 0) != CrSuccess || parent == node)
            {
                break;
            }

            node = parent;
        }

        return ids;
    }

    private static string? GetDeviceId(uint node)
    {
        var buffer = new StringBuilder(260);
        return CM_Get_Device_ID(node, buffer, buffer.Capacity, 0) == CrSuccess ? buffer.ToString() : null;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, int ulFlags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, int ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID(uint dnDevInst, StringBuilder buffer, int bufferLen, int ulFlags);
}
