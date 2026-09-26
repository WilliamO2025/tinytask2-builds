# TinyTask 2.0 - 0.2 release candidate

TinyTask 2.0 now includes a real Classic recorder/player on Windows and a native macOS Classic application. Advanced input isolation remains experimental and unavailable; it is not a promise of background Roblox control.

## Downloads

[Public release v0.2.0-rc4](https://github.com/WilliamO2025/tinytask2-builds/releases/tag/v0.2.0-rc4)

- [Windows installer](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc4/TinyTask2-Windows.Setup.exe)
- [Windows portable app](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc4/TinyTask2-Windows.exe)
- [macOS app ZIP](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc4/TinyTask2-macOS.zip)

Open the Windows installer for shortcuts. On Mac, unzip, move the app to Applications, open it, and grant Accessibility/Input Monitoring. Mac signing/notarization remains incomplete. The release also includes both Input Lab builds. Sessions are a Windows-only experimental preview requiring a configured session server; no public server is provided.

## RC4 playback and session preview

Windows adds prepared native input, absolute-timeline scheduling, lateness protection, shortcut combinations and per-task speed/loop settings. Experimental sessions provide installation identities, approved invitations/codes, Host/Member controls, Ready states and timestamped starts. These have been tested on loopback only. Reconnect recovery, Nearby/LAN discovery and real multi-computer timing validation remain unfinished. Mac includes the Classic timing update, not the Windows session features.

## Existing experimental input work

The Windows Advanced Support screen links to the separate **TinyTask2-Windows-Routing-Test.exe**. Its bounded mouse-only guard routes a software cursor while Roblox stays active, but physical clicks may still reach Roblox. It is not a background-control fix. The source includes a portable filter-policy prototype with 98 recovery/selection checks; no installable or signed driver or routing service is supplied. Classic Mode remains available. The routing test is unchanged from rc3; no driver is included.

## Download / launch

Windows: `dist/TinyTask2.Setup.exe` installs the user application with Start Menu and desktop shortcuts. `dist/win-x64/TinyTask2.exe` is portable. Both include their runtime. The separate tester is `dist/inputlab-win-x64/TinyTask2-InputLab.exe`, with installer `dist/TinyTask2-InputLab.Setup.exe`. No development tools are needed by users.

macOS: download `TinyTask 2.0.zip`, unzip on the Mac, move **TinyTask 2.0.app** into Applications and open it. Apple Silicon and Intel are included (macOS 13+). The optional **TinyTask 2.0 Input Lab.app** is a separate diagnostic tool. These bundles are ad-hoc signed, not Developer ID signed or notarized; public trusted distribution still needs those steps. Preserve the Mac-created ZIP when sharing; extracting/repacking on Windows may lose executable metadata.

See [release status and exact output paths](docs/RELEASE-0.2.md).

## Classic Mode

Open, Save, Record, Play, Pause/Resume, Stop, preferences, mouse motion/buttons, scrolling, keyboard transitions, recorded delays, speed (0.5/1/1.5/2/10/100/custom), loops and continuous playback are implemented. Classic uses the **system mouse and keyboard**. Playback preserves the recording's initial delay without adding a three-second lead-in. F8 records, F9 plays/pauses, F10 stops; recording/playback keys are configurable. Mac keyboards may require Fn.

Recording completion auto-saves to the macro library. Windows configuration/data is under `%LOCALAPPDATA%\TinyTask2\User`; the tester has its own `InputLab` data folder. Mac macros live in `~/Library/Application Support/TinyTask2/Macros`, with preferences in UserDefaults. Mac recording/hotkeys require Input Monitoring and Accessibility authorization; enable them from Preferences and restart if macOS requests it. Secure input can prevent capture. Closing exits and releases hooks/taps; minimizing keeps the tray/menu-bar app available.

Readable JSON is limited to 64 MB and 100,000 actions. Macro formats contain platform-specific key and coordinate information; cross-platform translation and original TinyTask `.rec` import are not implemented. Open/save preserve Windows JSON fields, but playback validates supported actions. There is no graphical action editor or parallel Classic playback.

## Advanced / Diagnostics

Advanced shows friendly target names, device observation and a guided setup. Normal test-button clicks and selected Raw Input cursor presses have separate counters. Both physical mice still affect the Windows cursor; the software pointer does not create independent OS focus. **Settings > Advanced > Diagnostics** retains raw IDs, HWND/PID, packet counts, controlled click/key/scroll/text/UIA probes, logs and export. No driver is installed automatically.

Diagnostics queue background messages selectively; only a visible reaction proves target support. Tests against Roblox did not establish background menu activation. Foreground scan-code Escape worked. See [Roblox evidence](docs/ROBLOX-2026-09-20.md), [HID investigation](docs/VIRTUAL-HID-STATUS.md), and [research](docs/INPUT-RESEARCH.md).

## Maintainer builds

`tools/build.ps1 -Publish` builds both Windows products. `macos/build.sh` builds native universal bundles on a Mac; the GitHub workflow runs it. End users do not run either script. The unrelated StudyMode project and original TinyTask installation are preserved.
