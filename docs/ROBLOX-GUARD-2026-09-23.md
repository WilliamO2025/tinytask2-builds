# Roblox mouse-only guard

Built `dist/route-roblox-probe/TinyTask2-NativeRouteProbe.exe` separately; existing
release outputs and the earlier physical-test binary remain intact.

The owned-window synthetic regression completed in 20,094 ms with 19 macro keys,
19 macro clicks, one routed control key/click, zero legacy A leakage, and zero
unhook failures. Evidence: `tests/route-roblox-guard-regression.json`.
This is a regression of the fixture, not a Roblox result.

CodeRabbit reviewed the three changed native-probe files and raised three issues:
missing preview launch path, overwritten report-write error status, and contradictory
mouse-only instructions. All were fixed. The revised native executable compiled
with `/W4 /WX`. Review: `tests/coderabbit-roblox-guard.ndjson`.

Actual Roblox testing is pending a visible safe menu and physical mouse movement.
Roblox was not running during preparation. The Computer Use helper failed with
`failed to connect native pipe ... (os error 2)` after retry and session reset;
the user must observe Roblox's reaction. Do not infer reaction from packet counts.

The guard sends no injected input to Roblox. It activates the single discovered
Roblox window, routes physical mouse movement to the owned fixture, and stops after
20 seconds, on focus loss, or on keyboard input. F10 is the emergency stop; the
independent hook-release watchdog is 22 seconds. The user should move only the mouse,
without clicking or typing, while observing Roblox. Raw-input isolation remains
unproven. No driver, security-setting change, or Roblox process modification is used.

The launched manual session writes `tests/roblox-guard-manual.json`. Its
`robloxReaction: null` must be supplemented by observed application behavior.
