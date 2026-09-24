# Bounded input-routing experiment

## Roblox mouse-only guard

The additional **Roblox mouse-only guard (20s)** button requires exactly one visible,
non-minimized `RobloxPlayerBeta.exe` window. First put Roblox in its home screen or
a harmless menu. Start the guard and move the physical mouse only. Do not click or
type. The test activates Roblox and routes physical mouse movement to the owned
background fixture. Left clicks are routed to the fixture if accidentally pressed,
but raw-input leakage is not ruled out. Any keyboard input (including F10), another
mouse button, focus loss, target disappearance, or 20 seconds ends the test.

No input is injected into Roblox, no process memory is accessed, and no driver is
installed. The existing 22-second independent hook-release watchdog remains active.
The report includes target PID/HWND, physical routed movement, cursor-change samples,
and global raw-input counts. `robloxReaction` is null: visible application reaction
must be observed separately. A stationary cursor is not proof of raw-input isolation.
This check does not establish keyboard routing, macro playback, or gameplay support.

Build separately using `./build-native.ps1 -Preview` to preserve the earlier binary.
Launch the preview from `dist/route-roblox-probe/TinyTask2-NativeRouteProbe.exe`.

This is an experiment, not a Roblox fix or a replacement release. Double-click
`dist/route-probe/TinyTask2-NativeRouteProbe.exe`. No installation, driver, or runtime
download is needed. Click **Start 20-second test**, release other keys/buttons, then
move the mouse, tap **A**, and left-click. The first five seconds are a baseline;
the remaining fifteen attempt to route physical input to the background work fixture
while tagged macro input continues in the focused fixture. F10, another key/button,
focus loss, or the time limit stops routing. An independent 22-second timer releases
hooks if the UI stops responding. Closing either window also stops the experiment.

The report is `%LOCALAPPDATA%/TinyTask2-RouteProbe/native-result.json`.
It records counts, not typed text. Keep both test windows open during the run.
Do not use this experiment for normal work: it only routes A and left mouse input.

## Measured result

On this PC, the isolated native keyboard control received two callbacks and its
fixture received F6. Registering Raw Input in that same process changed the result
to zero keyboard callbacks while F6 still reached the fixture. Both the managed
and native combined routing harnesses reproduced the missing callbacks. This is
an observed interaction on this machine, not a claim that every Windows build
has the same behavior or that the underlying OS cause has been diagnosed.

Moving Raw Input observation into a separate, read-only helper process fixed the
synthetic routing control: 19 macro keys and 19 macro clicks reached the focused
fixture; one separate control key and click reached the background fixture; zero
control A keys reached the focused fixture. Both hooks were removed at the end.
The helper exits when its controller disappears and is cleaned up on normal exit.

Synthetic success does **not** establish physical-device isolation. Raw Input
observation in this fixture also cannot prove what every game input path receives.
`physicalIsolationProven` remains false. Physical baseline activity must be present
before interpreting an absence of packets during routing. Roblox has not been
tested with this new route. Only compatible background work apps could use the
reversed-focus approach; arbitrary desktop work is not guaranteed.

## Maintainer checks

`./build-native.ps1 -Test` builds and runs the 20-second synthetic control using
MSVC and Win32 only. `native-control.cpp` is a smaller reproduction: no argument
tests a keyboard hook alone; any argument adds same-process Raw Input registration.
The deliberately failing comparison is research evidence, not a release gate.

The older managed harness is retained locally as failed experimental evidence;
the native executable is the current experiment. The normal TinyTask and Input Lab
release outputs are untouched.
