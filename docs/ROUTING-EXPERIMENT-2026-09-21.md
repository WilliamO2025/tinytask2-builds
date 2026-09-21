# Reversed-focus routing experiment

## Result so far

A new standalone, native Win32 experiment keeps the macro fixture focused while
routing a distinct input stream to a background work fixture. Raw Input observation
runs in a separate helper process. The released TinyTask applications are unchanged.

Two final synthetic runs passed: each delivered 19 macro F6 presses and 19 macro
clicks to the foreground fixture, one control A and one control click to the
background fixture, and zero control A presses to the foreground fixture. The final
run recorded zero UnhookWindowsHookEx failures. This is not physical-device or Roblox
acceptance. Synthetic control tags are a test aid, not an authentication boundary.

Local evidence: `tests/route-native-separated.json` and
`dist/route-probe/synthetic-result.json`. Earlier failed attempts are retained.

CodeRabbit raised two minor issues: closing the work fixture could leave the controller
open, and the test script could parse a stale report. Both were fixed. The synthetic
control passed again with a fresh unique report. A separate lifecycle check confirmed
that closing the work fixture exits the controller and leaves zero observer processes
(`tests/route-native-close.json`). Review: `tests/coderabbit-native-route-probe.ndjson`.

## Concrete issue found and addressed

The minimal `native-control.cpp` without Raw Input registration received two keyboard
hook callbacks and one F6 in its fixture. Adding Raw Input registration to that same
process changed the result to zero callbacks and one F6 in its fixture. Managed and
native combined harnesses also received no keyboard callbacks. Moving observation
into a separate process restored the callbacks (40 in the final routing run).

This is a controlled local comparison, not a universal Windows guarantee. The exact
Windows implementation cause remains uninstrumented. Launching a pointer test with
hidden STARTUPINFO also prevented valid macro click checks; the final tests used
normal visible windows. Moving SendInput off the managed UI thread did not resolve
the missing callbacks in that combined harness.

## Next acceptance gate

Open `dist/route-probe/TinyTask2-NativeRouteProbe.exe` normally and run the physical
20-second test. Baseline: move the mouse and tap A for five seconds. Routing phase:
continue moving, tapping A and left-clicking. Only these inputs are routed; other
keys/buttons or focus loss end the experiment. F10 stops it, with an independent
22-second safety timer. The report records packet and action counts, not typed text.

Check baseline activity, background work counts, foreground legacy leakage, and
foreground/global raw-input observation separately. Zero raw packets without baseline
activity is inconclusive. Even a clean fixture result would not prove Roblox isolation
or application-independent work routing. No new inputs have been sent to Roblox.

The remaining Roblox test, if justified by the physical result, is menu/Escape only.
Do not automate gameplay, modify the Roblox process, or call a driver install success
a compatibility success. No driver or OS protection changes were made.

## Other routes researched

- Microsoft documents only one active input desktop at a time within the interactive
  window station. A Win32 desktop or a Windows virtual desktop does not by itself
  supply a second simultaneously active input session.
  https://learn.microsoft.com/en-us/windows/win32/winstation/desktops
- InputInjector is another system input-injection API, not documented per-HWND
  independent focus. Restricted capabilities do not provide a generally available
  independent desktop replacement.
  https://learn.microsoft.com/en-us/uwp/api/windows.ui.input.preview.injection.inputinjector
- ASTER is a commercial multiseat candidate, but vendor documentation conflicts:
  the 2.70 release history advertises Secure Boot support while the v3 limitations
  page calls for disabling Secure Boot and Memory Integrity; installation history
  also lists Defender/firewall exceptions. It is not approved for this project's
  automatic installation under the user's security constraints. No package was
  installed or security setting changed. Roblox compatibility is unverified.
  https://ibiksoft.com/download-aster-multiseat-software/
  https://dokwiki.ibiksoft.com/en/v3/core/faq/faq_limitations

Microsoft's hook documentation notes silent removal after a callback timeout. That
is an additional reason this experiment cannot be advertised as strict isolation.
https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc
