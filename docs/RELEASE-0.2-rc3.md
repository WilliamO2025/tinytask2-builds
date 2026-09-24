# TinyTask 2.0 v0.2 Release Candidate 3

This update adds experimental routing downloads and clearer Advanced Support status.
**It does not provide a working Roblox mouse blocker or an installable input driver.**

## Downloads

- Windows: `TinyTask2-Windows.Setup.exe` installs the normal app and shortcuts.
- Windows portable: `TinyTask2-Windows.exe`.
- Windows diagnostics: `TinyTask2-Windows-InputLab.exe`.
- Separate experiment: `TinyTask2-Windows-Routing-Test.exe`.
- Mac users: download **`TinyTask2-macOS.zip`**.
- Optional Mac diagnostics: `TinyTask2-macOS-InputLab.zip`.
- `SHA256SUMS.txt` contains artifact integrity hashes.

Windows builds include their runtime. No terminal or development tools are required.
Keep the installer filename ending in `.Setup.exe`; it selects installation mode.
Windows executables are unsigned and may trigger Windows reputation warnings.

## Classic Mode

Mouse movement/click/scroll recording, keyboard recording, timing, playback,
pause/stop, playback speed, loops, save/load and preferences remain available.
Classic playback uses the real system mouse and keyboard. Windows and macOS are
supported; Advanced work does not change the Classic input model.

## Advanced Mode — experimental

Target selection, second-mouse detection, virtual cursor experiments, diagnostics
and harmless Roblox compatibility testing remain available. Independent Windows
cursor/input isolation is not complete. Roblox background input is not verified.

The new Advanced Support screen explains the filter prototype's status and links to
this download page. The separate routing test has a **Roblox mouse-only guard (20s)**
button. Use a safe Roblox home/menu screen; move the mouse only. Any key, including
F10, stops it. Do not use it for gameplay or normal work. Raw mouse input may still
reach Roblox even when the normal cursor stays still. The guard injects no Roblox
input and changes no drivers or Windows protections.

The source contains a portable device-filter policy prototype with 98 passing checks
for device selection, bounded buffering and recovery. It is **not a Windows driver**.
A kernel adapter, secured routing service, real hardware validation and Microsoft-
trusted signing remain necessary. Installation stays disabled; no drivers are bundled.

## macOS installation

1. Download `TinyTask2-macOS.zip` and unzip it.
2. Move `TinyTask 2.0.app` into Applications.
3. Open the app normally.
4. Grant Accessibility and Input Monitoring when requested. macOS may require
   approval in System Settings and a restart of the app; it cannot grant itself access.

These universal Intel/Apple Silicon macOS 13+ ZIPs are unchanged from rc2, including
the native permission request flow. They are ad-hoc signed, **not Developer ID signed
or notarized**. Gatekeeper may block normal first launch; trusted signing remains
unfinished. Physical macOS input acceptance was not retested during this Windows pass.

## Validation

Windows: 15 existing UI/theme checks and 9 Classic checks passed. Filter policy:
98 checks passed. The native owned-window routing regression passed with 19 macro
keys/clicks, one routed control key/click and zero unhook failures. These results do
not prove Roblox isolation. CodeRabbit review and fixes are recorded in the source.

Existing rc1 and rc2 releases remain available and unchanged.
