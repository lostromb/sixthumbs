# Sixthumbs

One virtual Xbox 360 pad driven by one or more physical pads (local or over TCP). Several people can share player 1 — six thumbs.

## What it does

The same WPF app is a patchbay:

- **Host:** reads local SDL3 pads (and optional TCP clients), maps/masks each source, merges them, and submits one virtual Xbox 360 controller through ViGEmBus.
- **Client:** reads local pads, maps/masks, and forwards the merged state to a host. No virtual pad on the client.

Typical setups:

- One PC: DirectInput / DualShock / whatever SDL sees → virtual X360
- One host + one remote client over the Internet (you provide connectivity)
- Several local pads all mapped onto player 1
- Host plus several networked clients

**Merge:** buttons OR together; analog axes keep the sample with the largest magnitude; triggers take the max. Idle zeros lose.

**Rumble** is not forwarded in v1.

## Requirements

- Windows x64, .NET 8
- [ViGEmBus](https://github.com/ViGEm/ViGEmBus/releases) installed on the **host** (the machine running the game). The driver project is archived; the last release still works for this.
- Optional: [HIDHide](https://github.com/Nefarius/HidHide) if games see both your physical XInput pads and the virtual one

Build:

```
dotnet build src/Sixthumbs.App/Sixthumbs.App.csproj -c Release
```

Run `src/Sixthumbs.App/bin/Release/net8.0-windows/Sixthumbs.exe`.

## Host vs client

1. On the game machine, choose **Host**, click **Start**. Sixthumbs connects a virtual X360 pad immediately so it has a better chance of landing in XInput slot 0 (player 1).
2. Optionally check **Accept TCP clients**, set a listen port (default `26180`) and a shared password, then port-forward that TCP port or use a VPN.
3. On another PC, choose **Client**, enter the host address and port, same password, **Start**. Map or mute local controls as needed. The client sends full pad snapshots (~125 Hz) as length-prefixed binary frames (`STH1` handshake).

There is no NAT hole-punching. If you are not on the same LAN, use port forwarding, Tailscale, etc.

## Mapping

Select a device and use the patchbay grid:

- **On** enables that virtual control for this source (turn everything off except Left X/Y for “this player only moves the left stick”).
- **From source control** remaps (for example pad B’s face buttons onto the virtual A/B/X/Y).
- **Mute** ignores the whole device.
- Unmapped DirectInput sticks also get a **raw axis/button → X360** list. That layout is per device (SDL GUID + name), stored in `%AppData%\Sixthumbs\settings.json`, and is restored on reconnect. It is not part of a preset.

**Save Preset** / **Load Preset** / **Reset to Defaults** only affect the X360→X360 patchbay (game-specific). A wheel’s raw DInput map stays with the hardware. Preset dialogs default to `%AppData%\Sixthumbs\Presets`.

## Physical pads competing with the virtual pad

ViGEm cannot pick player 1. After Start, the UI shows the assigned XInput user index (0 = player 1) and warns if it is not 0.

SDL will also open real Xbox pads, so a game can see **physical + virtual**. The usual fix:

1. Install HIDHide.
2. Hide the physical controllers from the game.
3. **Whitelist** `Sixthumbs.exe` so this app can still read them.
4. Restart the host role in Sixthumbs, then launch the game.

Sixthumbs filters ViGEm’s virtual pad back out of SDL input by name (`Virtual Xbox 360 Controller`), device path / PnP parent (`ViGEmBus`), and matching XInput user index. Physical Xbox pads are left alone.

## Protocol (v1)

Handshake: magic `STH1`, version `1`, SHA-256 of the password (32 zero bytes if empty), UTF-8 name. Empty host password accepts anyone.

Then frames: `uint32` length + payload (`type=1`, `uint32` sequence, 12-byte packed X360 state). If a client goes silent for 500 ms, that source is treated as released.
