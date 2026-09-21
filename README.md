# TinyTask 2.0

A compact Classic macro recorder for Windows and macOS, with a separate experimental Input Lab.

## Downloads

[Download v0.2.0-rc1 from GitHub Releases](https://github.com/WilliamO2025/tinytask2-builds/releases/tag/v0.2.0-rc1).

- [Windows installer](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc1/TinyTask2-Windows.Setup.exe)
- [Windows portable app](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc1/TinyTask2-Windows.exe)
- [TinyTask 2.0 for macOS](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc1/TinyTask2-macOS.zip)
- [Windows Input Lab](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc1/TinyTask2-Windows-InputLab.exe)
- [macOS Input Lab](https://github.com/WilliamO2025/tinytask2-builds/releases/download/v0.2.0-rc1/TinyTask2-macOS-InputLab.zip)

Windows: run the installer for Start Menu and desktop shortcuts, or open the portable EXE. The runtime is included. Builds are unsigned and may trigger SmartScreen.

Mac: unzip on your Mac, move TinyTask 2.0.app to Applications, open it, and grant Accessibility/Input Monitoring permissions. Requires macOS 13+, Intel or Apple Silicon. No developer tools are needed. The app is ad-hoc signed, not Developer ID signed or notarized; Gatekeeper may require [Open Anyway](https://support.apple.com/102445). Physical Mac input acceptance testing remains outstanding.

## Classic Mode

Record mouse movement/clicks, scrolling, keyboard input and timing; open/save JSON macros; play, pause, stop, change speed and loop. Classic playback controls the system mouse and keyboard. F8 records, F9 plays/pauses, F10 stops; shortcuts are configurable (Mac may require Fn). Preferences support light/dark appearance. Macros are platform-specific; original TinyTask .rec import and a graphical action editor are not included.

## Advanced Mode is experimental

Windows target selection, second-mouse observation, software cursor tests, diagnostics and harmless Roblox probes are available. Independent physical cursor/focus isolation and user-keyboard routing are not complete. Background Roblox compatibility is not established. No driver is bundled or silently installed. The separate Mac Input Lab is experimental.

## Maintainer builds

Windows: tools/build.ps1 -Publish with .NET 8 SDK. macOS: bash macos/build.sh on a Mac. These are maintainer steps; end users download the packaged apps. Recordings may contain private text and should not be uploaded. Only TinyTask source/build files are maintained here.
