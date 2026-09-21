# Advanced setup continuation - 21 September 2026

## Public downloads repaired

Repository: https://github.com/WilliamO2025/tinytask2-builds (public with explicit owner approval).
Release: https://github.com/WilliamO2025/tinytask2-builds/releases/tag/v0.2.0-rc1 .
The interrupted release was still a draft. All five application files and SHA256SUMS.txt were uploaded; publishing the draft repaired the README links. All six URLs returned HTTP 200 without GitHub authentication (`tests/public-release-downloads.json`). The original local release artifacts still match every SHA-256 in `dist/release-manifest.json`.

The Windows installer deliberately keeps `.Setup.exe` in its name because the existing installer entry point recognizes that suffix. Do not rename it to `-Setup.exe` without changing and testing the launcher.

## Separate Advanced preview

`dist/advanced-preview/TinyTask2.exe` is a standalone preview, not an rc1 replacement and not installed over the normal app. Close the normal application before running it so hotkeys are available. It currently shares the normal app's settings/library; new mouse-choice fields are backward-compatible.

Implemented: actionable Input Support, Choose Mouse, Set Up/Test Cursor and Keyboard Configure rows; persistent macro/user mouse paths; connected/disconnected status; best-effort Windows-friendly mouse names; direct cursor test for saved connected assignments; explicit Disable test cursor; Ctrl+Alt+F12 stop; missing devices are never silently replaced. Raw Input handles are resolved from device paths on setup, not persisted across reboots. Finish saves choices; cancelling discards edits. Existing Classic engine and Input Lab remain intact.

The input-support screen explains the actual missing prerequisites, checks connected device counts and disables installation when no component is approved. It does not pretend a working installer exists. It does not download, elevate, install, remove, or filter devices. Standard installation remains unchanged; an Advanced installation choice is not enabled prematurely.

Tests: `tests/advanced-preview/ui.json` (14 checks) and `classic.json` (9 controlled native Classic checks). Dark preview screenshots are in that directory. CodeRabbit reviewed the Advanced setup source and raised 0 issues (`tests/coderabbit-advanced-setup.ndjson`). The later missing-device selection regression check also passes. The review does not certify a driver or installer that has not been implemented.

## Input architecture blockers

Microsoft VHF is an OS framework, not a ready-to-install independent-input component. A HID source driver must create reports and manage lifetime. Our own driver still needs implementation, WDK builds, Microsoft-trusted signing and device-level acceptance. There is no signing account/certificate available in this session. [VHF](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-) and [signing requirements](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/code-signing-reqs).

The Microsoft UMDF vhidmini2 sample is another supported source starting point, not a pre-signed TinyTask driver: https://github.com/microsoft/Windows-driver-samples/tree/main/hid/vhidmini2 .

HID reports alone do not target HWNDs, suppress a physical mouse, create a second Windows foreground focus, or stop a physical keyboard from reaching foreground Roblox. Per-device suppression/routing would require a separately validated design and recovery mechanism. Raw Input is observation; ordinary window messages remain target-dependent. No anti-cheat bypass, process injection, driver-signature bypass or security downgrade is proposed.

## Alternative inspected, NOT installed

Official archived Ryochan7/FakerInput v0.1.1 is an MIT-licensed UMDF candidate. MSI: https://github.com/Ryochan7/FakerInput/releases/download/v0.1.1/FakerInput_Setup_0.1.1_x64.msi . Source/license: https://github.com/Ryochan7/FakerInput . SHA256: `4C0AEFB7340051A91D606776243298B5CD1143EF5508BBAE6800C474F9ED0840`.

MSI and DLL Authenticode signatures are valid under Ryodigi Solutions LLC. The driver catalog and DLL/catalog binding pass Windows SDK SignTool `/pa` verification. Catalog verification under its extracted suffixed filename initially gave a misleading HashMismatch; retaining the proper `.cat` filename produced valid verification. This is user-mode Authenticode evidence, not proof of Microsoft hardware certification, present-OS installation compatibility, safe cleanup, or Roblox support.

Source inspection also found unchecked output-buffer retrieval before a copy in Queue.c and subtraction of a header size before a visible minimum-size check. These are concerns requiring validation against the shipped binary/version, not a demonstrated exploit. The source is archived, documentation is sparse, and no device watchdog/recovery acceptance has been established. This candidate is therefore NOT approved for automatic installation. The user was asked whether to pursue it or wait for our own signed component; that choice remains pending at the time of this report.

No third-party component was installed, no physical input was blocked, and no new HID reports or Roblox inputs were sent. Installing a driver would not by itself satisfy the independent-input goal.

## Remaining work

After selecting and validating a component: implement a pinned HTTPS download with bounded size, expected hash and trusted signer/catalog verification; explicit consent and Windows elevation; narrow managed-device installation; device enumeration and harmless fixture tests; physical-input/emergency-stop acceptance; crash/reboot/uninstall recovery. Only then enable automatic installation and the Advanced installer option. Retest Roblox menu/Escape foreground/background behavior afterward, without treating driver enumeration as compatibility evidence.

An initial Classic assertion expected lowercase text while Caps Lock was active. The harness now reads the current toggle state without changing it; all 9 checks pass. The initial report is retained as classic-first-case-assumption.json. This test-only correction and the missing-device selection check followed the reviewed setup diff.

## Driver source hardening follow-up

The released FakerInput v0.1.1 source was checked and report-buffer defects reproduced with WDF doubles. The patch in `drivers/fakerinput-fix` passes 22 regression checks; the original source reproduces 3 defects. CodeRabbit's final focused review raised 0 issues. These are isolated source tests, not a full WDK build, signed driver, installation test or Roblox compatibility result. No driver was installed. See that folder's README for reproduction and remaining acceptance requirements. Existing release hashes remain unchanged.
