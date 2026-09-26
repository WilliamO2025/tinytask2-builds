# TinyTask 2.0 — complete requirements audit

Date: September 25, 2026. Original audit retained as the implementation checklist. Statuses updated after the Windows playback, keybind, profile and session implementation pass; no release published.

Scope: all TinyTask requirements in this conversation, the original cross-platform attachment and continuation attachments, the 52-section master specification, the low-latency/LAN supplement, and both later timing corrections. Repeated equivalent requests are counted once. Different behaviors remain separate rows. The two low-latency/LAN attachments are identical and counted once.

The later immutable-timeline correction supersedes the earlier suggestion to adjust recorded delays: corrections must converge toward original deadlines, never rewrite the recording. The example speed range 0.1–10 was conditional, not a mandatory replacement for earlier 100x support. Future optional Co-Host implementation, future automatic trust acceptance and future macro-sharing are not counted as current requirements.

**IMPLEMENTED** means the stated behavior has actual code at the scope described. It does not automatically mean cross-platform hardware validation or inclusion in the public release. **PARTIALLY IMPLEMENTED** means a real subset/prototype exists but the requested behavior, integration or validation is incomplete. **NOT IMPLEMENTED** means the required behavior is absent. A related feature, disabled button, static status, or absence of networking is not credited as implementation.

The audit covers current local source. Public rc3 is older: the latest timing changes are not published. Latest Windows checks: 13 timeline assertions, 21 UI checks, 14 Classic tests, 26 server checks and 11 desktop session client checks. New session tests use loopback, not separate physical computers. Latest macOS timing edits are unbuilt. Implementation statuses use the latest saved test reports. Public download evidence is historical, not a fresh online check.

## Evidence key

Paths are relative to `C:/Users/Admin/Downloads/StudyMode/tinytask2`. A reference in every implemented/partial row identifies the responsible file and, where useful, method/class. Source filenames are not proof of hardware compatibility.

| Key | Actual file / principal classes |
|---|---|
| W | [src/TinyTask.Diagnostics/ClassicEngine.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/ClassicEngine.cs): ClassicEngine, MacroDocument, MacroAction |
| H | [src/TinyTask.Diagnostics/HomeWindow.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/HomeWindow.cs): HomeWindow, HomePreferences, FriendlyTarget |
| T | [src/TinyTask.Diagnostics/PlaybackTimeline.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/PlaybackTimeline.cs): PlaybackTimeline |
| M | [macos/ClassicEngine.swift](C:/Users/Admin/Downloads/StudyMode/tinytask2/macos/ClassicEngine.swift): ClassicEngine, MacroDocument, MacroAction |
| U | [macos/TinyTask.swift](C:/Users/Admin/Downloads/StudyMode/tinytask2/macos/TinyTask.swift): TinyTaskApp |
| D | [src/TinyTask.Diagnostics/MainWindow.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/MainWindow.cs): MainWindow, CursorWindow |
| R | [src/TinyTask.Diagnostics/Routing.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/Routing.cs): Routing |
| RAW | [src/TinyTask.Diagnostics/RawInput.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/RawInput.cs): RawInput, Device, RawSample |
| SW | [src/TinyTask.Diagnostics/SetupWizard.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/SetupWizard.cs): SetupWizard |
| WP | [src/TinyTask.Diagnostics/WindowPlayback.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/WindowPlayback.cs): WindowPlayback |
| AS | [src/TinyTask.Diagnostics/AdvancedInputSetup.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/AdvancedInputSetup.cs): AdvancedInputSetup |
| TH | [src/TinyTask.Diagnostics/UiTheme.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/UiTheme.cs): UiTheme |
| ID | [src/TinyTask.Diagnostics/AppIdentity.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/AppIdentity.cs): AppIdentity |
| SET | [src/TinyTask.Diagnostics/Setup.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/Setup.cs): Setup |
| NATIVE | [src/TinyTask.Diagnostics/Native.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/Native.cs): Native, Target |
| ROB | [src/TinyTask.Diagnostics/RobloxTest.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/RobloxTest.cs): RobloxTest |
| DUAL | [src/TinyTask.Diagnostics/DualTest.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/DualTest.cs): DualTest |
| BROWSER | [src/TinyTask.Diagnostics/BrowserTest.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/BrowserTest.cs): BrowserTest |
| PROBE | [tools/InputRouteProbe/route-native.cpp](C:/Users/Admin/Downloads/StudyMode/tinytask2/tools/InputRouteProbe/route-native.cpp): native routing/Roblox guard experiment |
| DRV | [drivers/mouse-filter-prototype/driver.cpp](C:/Users/Admin/Downloads/StudyMode/tinytask2/drivers/mouse-filter-prototype/driver.cpp): KMDF filter prototype |
| POL | [drivers/mouse-filter-prototype/filter_policy.h](C:/Users/Admin/Downloads/StudyMode/tinytask2/drivers/mouse-filter-prototype/filter_policy.h): filter policy |
| BROKER | [drivers/mouse-filter-prototype/broker.cpp](C:/Users/Admin/Downloads/StudyMode/tinytask2/drivers/mouse-filter-prototype/broker.cpp): bounded routing broker |
| VHF | [docs/VIRTUAL-HID-STATUS.md](C:/Users/Admin/Downloads/StudyMode/tinytask2/docs/VIRTUAL-HID-STATUS.md): investigation and package evidence |
| RESEARCH | [docs/INPUT-RESEARCH.md](C:/Users/Admin/Downloads/StudyMode/tinytask2/docs/INPUT-RESEARCH.md): platform/input investigation |
| FIX | [drivers/fakerinput-fix/README.md](C:/Users/Admin/Downloads/StudyMode/tinytask2/drivers/fakerinput-fix/README.md): alternative driver investigation |
| MB | [macos/build.sh](C:/Users/Admin/Downloads/StudyMode/tinytask2/macos/build.sh): native universal app packaging/tests |
| ML | [macos/InputLab.swift](C:/Users/Admin/Downloads/StudyMode/tinytask2/macos/InputLab.swift): Mac diagnostic application |
| BUILD | [tools/build.ps1](C:/Users/Admin/Downloads/StudyMode/tinytask2/tools/build.ps1): Windows build/publish |
| CI | [.github/workflows/macos.yml](C:/Users/Admin/Downloads/StudyMode/tinytask2/.github/workflows/macos.yml): Mac build workflow |
| UI | [src/TinyTask.Diagnostics/UiTests.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/UiTests.cs): UI checks |
| CT | [src/TinyTask.Diagnostics/ClassicTests.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/src/TinyTask.Diagnostics/ClassicTests.cs): controlled Classic tests |
| TT | [tests/TimelineChecks/Program.cs](C:/Users/Admin/Downloads/StudyMode/tinytask2/tests/TimelineChecks/Program.cs): timing assertions |
| README | `README.md`: downloads and product limits |
| REL | [docs/RELEASE-0.2.md](C:/Users/Admin/Downloads/StudyMode/tinytask2/docs/RELEASE-0.2.md): release record |
| PUB | [tests/rc3/public-downloads.json](C:/Users/Admin/Downloads/StudyMode/tinytask2/tests/rc3/public-downloads.json): saved public download checks |
| REVIEW | [tests/coderabbit-timeline.ndjson](C:/Users/Admin/Downloads/StudyMode/tinytask2/tests/coderabbit-timeline.ndjson): last review, 0 issues |
| LAUNCH | [tools/test-launch.ps1](C:/Users/Admin/Downloads/StudyMode/tinytask2/tools/test-launch.ps1) and `tests/launch-coexistence*.json`: coexistence checks |

Windows session client/server, installation identity, invitations, Ready state and clock sampling now exist in SessionConnection.cs, SessionWindow.cs and TinyTask.SessionServer. Public hosting, Mac sessions, reconnect recovery and LAN discovery remain unfinished. Loopback tests do not prove cross-device timing or physical input isolation.

## Recording and macro management

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 1 | Record mouse movement | IMPLEMENTED | W.MouseHook/Append; M.receive. |
| 2 | Record mouse coordinates | IMPLEMENTED | W/M.MacroAction X/Y fields. |
| 3 | Record left click | IMPLEMENTED | W.MouseHook; M.receive. |
| 4 | Record right click | IMPLEMENTED | W.MouseHook; M.receive. |
| 5 | Record middle click | IMPLEMENTED | W.MouseHook; M.receive. |
| 6 | Record mouse down and up separately | IMPLEMENTED | W/M action models and capture handlers. |
| 7 | Record vertical/horizontal scrolling | IMPLEMENTED | W.MouseHook; M.receive scroll fields. |
| 8 | Record keyboard presses | IMPLEMENTED | W.KeyboardHook; M.receive. |
| 9 | Record keyboard releases | IMPLEMENTED | W.KeyboardHook; M.receive. |
| 10 | Record inter-action delays | IMPLEMENTED | W.Append/Stop; M.receive/stop. |
| 11 | Record target window/application information | PARTIALLY IMPLEMENTED | W.TargetProcess/Coordinates and Record(Target); WP. H does not expose it; Mac lacks equivalent. |
| 12 | Record button | IMPLEMENTED | H.ToggleRecord; U.record. |
| 13 | Stop button ends recording | IMPLEMENTED | H.StopAll; U.stop. |
| 14 | Save readable JSON recordings | IMPLEMENTED | H.SaveCopy; W/M.MacroDocument; U.save. |
| 15 | Load recordings | IMPLEMENTED | H.OpenMacro/LoadDocument; U.open. |
| 16 | Store multiple saved recordings | IMPLEMENTED | H.LibraryDirectory/EngineChanged; U.library/update. |
| 17 | In-app macro library | PARTIALLY IMPLEMENTED | H/U open a saved-macros folder; no in-app management list. |
| 18 | Rename saved macro/task | PARTIALLY IMPLEMENTED | H.SaveMacro/U.save allow choosing filenames; no Rename command updating task identity/name. |
| 19 | Delete saved macro through app | NOT IMPLEMENTED | No app delete command; file manager deletion is not this feature. |
| 20 | Duplicate saved task | PARTIALLY IMPLEMENTED | H.SaveCopy/U.save permit Save As copies; no Duplicate action or automatic Copy name. |
| 21 | Editor showing action type/time/delay/coordinates/key/button | NOT IMPLEMENTED | No action editor. |
| 22 | Delete individual actions | NOT IMPLEMENTED | No action editor. |
| 23 | Edit recorded delays | NOT IMPLEMENTED | No action editor. |
| 24 | Change recorded coordinates | NOT IMPLEMENTED | No action editor. |
| 25 | Insert actions | NOT IMPLEMENTED | No action editor. |
| 26 | Duplicate actions | NOT IMPLEMENTED | No action editor. |
| 27 | Reorder actions | NOT IMPLEMENTED | No action editor. |
| 28 | Atomic/safe macro saving | IMPLEMENTED | H.SaveCopy temporary replacement; U.save atomic write. |
| 29 | Preserve unknown Windows JSON fields on open/save | IMPLEMENTED | H keeps macroJson; UI round-trip assertion. |
| 30 | Auto-save completed recordings and recover last recording | IMPLEMENTED | H.EngineChanged/Loaded; U.update/launch. |

## Playback, speed, loops and task profiles

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 31 | Play recorded/loaded macro | IMPLEMENTED | W.Play/M.play; H/U controls. |
| 32 | Pause/resume playback | IMPLEMENTED | W.PauseResume; M.pauseResume. |
| 33 | Stop playback and release held input | IMPLEMENTED | W.Stop/ReleaseHeld; M.stop/release. |
| 34 | Dedicated Restart control | NOT IMPLEMENTED | Stop then Play is possible; no Restart command. |
| 35 | Run once | IMPLEMENTED | W/M loops=1; H/U loop fields. |
| 36 | Repeat chosen number of times | IMPLEMENTED | W.Play/M.tick; H/U count fields. |
| 37 | Continuous playback until stopped | IMPLEMENTED | W/M continuous; H/U toggle. |
| 38 | 0.5x preset | IMPLEMENTED | H.Layout; U speed combo. |
| 39 | 1x preset | IMPLEMENTED | H.Layout; U speed combo. |
| 40 | 1.5x preset | IMPLEMENTED | H.Layout; U speed combo; local source addition, not rc3. |
| 41 | 2x preset | IMPLEMENTED | H.Layout; U speed combo. |
| 42 | 10x preset | IMPLEMENTED | H.Layout; U speed combo. |
| 43 | 100x preset | IMPLEMENTED | H.Layout; U speed combo. |
| 44 | Custom decimal speed | IMPLEMENTED | H.PlayMacro/W.Play; U.playPause/M.play. |
| 45 | Presets update same value as manual speed | IMPLEMENTED | H.Speed and U.speed combos feed same Play path. |
| 46 | Numeric-only speed field | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 47 | Reject letters during speed entry | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 48 | Reject unsupported symbols/pasted syntax | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 49 | Validate speed before applying playback | IMPLEMENTED | H/W and U/M playback guards. |
| 50 | Visible rejection of nonfinite/out-of-range speed | IMPLEMENTED | H/W and U/M reject outside 0.01–1000. No claim that 900 is rejected. |
| 51 | Extreme speeds cannot destabilize playback | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 52 | Validate whole-number loop count | IMPLEMENTED | H/W and U/M accept 1–1000000. |
| 53 | Remove added Windows three-second local delay | IMPLEMENTED | W.Play default startDelaySeconds=0; H caption; local source only. |
| 54 | Remove added macOS three-second local delay | PARTIALLY IMPLEMENTED | M.play offsets[0], U caption; latest change unbuilt/untested on Mac. |
| 55 | Preserve recording's own first-event delay | IMPLEMENTED | T.Due/W.Play; TT assertion. |
| 56 | Remember playback speed per task | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 57 | Remember loop behavior per task | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 58 | Load saved configuration when switching tasks | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 59 | Profile stores inputs and meaningful task name | PARTIALLY IMPLEMENTED | W/M document name/actions exist; proper rename/profile workflow absent. |
| 60 | Profile stores other task-specific settings | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/PlaybackSettings.cs: decimal text/paste validation and JSON per-task playback profile; Windows tested, Mac equivalent pending. |
| 61 | Prefer global Start/Stop key configuration | IMPLEMENTED | H.HomePreferences/U defaults keep existing shortcuts global. |

## Shortcuts and preferences

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 62 | Default F8 records/finishes | IMPLEMENTED | H.RegisterControls/WndProc; M/U callbacks. |
| 63 | Default F9 plays/pauses | IMPLEMENTED | H.WndProc; M/U callbacks. |
| 64 | Default F10 stops | IMPLEMENTED | W.KeyboardHook/H.RegisterControls; M.receive. |
| 65 | Customize Record shortcut | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 66 | Customize Start/Playback shortcut | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 67 | Customize Stop shortcut | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 68 | Customize optional Leave Session shortcut | NOT IMPLEMENTED | No sessions/binding. |
| 69 | Ctrl+F8 combination | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 70 | Shift+F6 combination | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 71 | Alt+Q combination | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 72 | Ctrl+Shift+P/multiple-modifier combinations | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 73 | Persist all custom shortcuts | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 74 | Click field to capture next key/combination | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 75 | Detect shortcut conflicts and require resolution | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 76 | Reset each shortcut to default | PARTIALLY IMPLEMENTED | H and src/TinyTask.Diagnostics/Shortcut.cs: captured modifier combinations, collision checks, reset and persistence on Windows; Mac and optional Leave binding pending. |
| 77 | Local emergency Stop overrides session Host | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 78 | Leave Session affects only self and does not quit app | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 79 | Always on Top toggle | IMPLEMENTED | H.CreateSettingsWindow; U.preferences. |
| 80 | Minimize/background operation with tray/menu bar | IMPLEMENTED | H.StateChanged/NotifyIcon; U.NSStatusItem/native minimization. |
| 81 | Tray quick Record/Play/Stop | PARTIALLY IMPLEMENTED | U.makeMenus has all; H tray only Open/Stop/Exit. |
| 82 | Persist settings in per-user configuration storage | IMPLEMENTED | H.SavePreferences/D.DataDirectory; U.UserDefaults. |
| 83 | Start with operating system | NOT IMPLEMENTED | No startup preference/registration. |
| 84 | Mouse-recording toggle | NOT IMPLEMENTED | No independent capture toggle. |
| 85 | Keyboard-recording toggle | NOT IMPLEMENTED | No independent capture toggle. |
| 86 | Movement precision setting | NOT IMPLEMENTED | W fixed 8ms throttle, not a setting. |
| 87 | Delay-recording setting | NOT IMPLEMENTED | Always captured. |
| 88 | Remember default speed globally | IMPLEMENTED | H.SavePreferences; U.saveSettings. |
| 89 | Remember default looping globally | IMPLEMENTED | H.SavePreferences; U.saveSettings. |
| 90 | Show captions toggle | NOT IMPLEMENTED | No caption visibility preference. |
| 91 | Custom toolbar option | NOT IMPLEMENTED | No toolbar configuration. |
| 92 | Restore/default toolbar option | NOT IMPLEMENTED | No toolbar reset. |
| 93 | Dedicated TinyTask-style Playback/Recording menus | PARTIALLY IMPLEMENTED | H toolbar/preferences and U File/app menus; requested complete menus absent. |
| 94 | Recording-started notification | NOT IMPLEMENTED | Status text exists; requested notification mechanism absent. |
| 95 | Recording-stopped notification | NOT IMPLEMENTED | No notification delivery. |
| 96 | Playback-started notification | NOT IMPLEMENTED | No notification delivery. |
| 97 | Playback-finished notification | NOT IMPLEMENTED | No notification delivery. |
| 98 | Notifications preference | NOT IMPLEMENTED | No notification setting. |

## Account-free identity

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 99 | Local use without sign-in/signup/email/password/phone/account screens | IMPLEMENTED | H/U launch directly into local app. |
| 100 | Ask username only on first Invite/Join attempt | PARTIALLY IMPLEMENTED | SessionWindow requests and stores a username in the session screen, not precisely on first Invite/Join. |
| 101 | Explain username as name others see | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 102 | Save username locally without repeated prompts | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 103 | Change username in Settings | PARTIALLY IMPLEMENTED | SessionWindow permits editing username after disconnect; main Settings integration pending. |
| 104 | Generate permanent installation-specific internal UUID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 105 | Internal UUID hidden and normally not editable | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 106 | Keep internal ID when public information changes | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 107 | Public shareable TinyTask ID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 108 | Customize public ID subject to availability | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 109 | Enforce global public-ID uniqueness | PARTIALLY IMPLEMENTED | SessionState persists and enforces Public ID uniqueness on one server; no authoritative online service or multi-instance registry deployed. |
| 110 | Change public ID without changing internal ID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 111 | Separate username/public ID; allow similar usernames | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 112 | Friendly Device Name distinct from username/public ID | NOT IMPLEMENTED | No discovery name model. |
| 113 | Change Device Name in Settings | NOT IMPLEMENTED | No setting. |
| 114 | Multiplayer extends existing simple UI rather than social/account app | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |

## Invitations, codes and membership

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 115 | Look up another installation by public ID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 116 | Invite to Session action | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 117 | Confirm recipient username/ID with Yes/No before sending | PARTIALLY IMPLEMENTED | SessionWindow confirms typed recipient ID before sending; recipient username lookup preview pending. |
| 118 | Incoming invitation identifies sender username/ID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 119 | Incoming Accept/Decline actions | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 120 | Explicit approval prevents silent connections | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 121 | Host generates temporary session code | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 122 | Join Session by code | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 123 | Copy Invite to clipboard | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 124 | Private sessions by default | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 125 | Valid code still requires Host approval | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 126 | Session code expires when session ends | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 127 | Exactly one Host per session | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 128 | Sender normally Host; receiver Member | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 129 | Merge crossed/simultaneous invites into one session | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 130 | First valid server-arriving invite determines Host | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 131 | Member list and per-member dropdown | PARTIALLY IMPLEMENTED | SessionWindow member list has task/Ready/ping/jitter and explicit Host/Kick buttons; per-member dropdown pending. |
| 132 | Host kicks member | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 133 | Make Host action | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 134 | Confirm host transfer with recipient identity | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 135 | Transfer atomically promotes new/demotes old Host | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 136 | Add local/session nickname | NOT IMPLEMENTED | No nickname model. |
| 137 | Change nickname | NOT IMPLEMENTED | No nickname editing. |
| 138 | Remove nickname | NOT IMPLEMENTED | No nickname removal. |
| 139 | Relevant Add versus Change/Remove menu choices | NOT IMPLEMENTED | No member dropdown. |
| 140 | Nickname does not replace actual username | NOT IMPLEMENTED | No nickname/identity separation. |
| 141 | Session survives Host disconnect | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 142 | Deterministically elect one replacement Host | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 143 | Member controls own Ready/Not Ready | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 144 | Host sees everyone's Ready state | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 145 | Require Everyone Ready Before Start setting | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 146 | Enforce require-ready ON; allow Host start when OFF | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 147 | Approximate ping per member | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 148 | Synced/Synchronizing/Poor Connection/Reconnecting/Lost/Disconnected | PARTIALLY IMPLEMENTED | SessionConnection detects loss and stops local input; SessionState removes disconnected member/elects host. No reconnect grace/state recovery. |
| 149 | Lock/unlock session | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 150 | Locked session keeps members but blocks new requests/codes | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 151 | Recent/Trusted users list | NOT IMPLEMENTED | No trusted-user storage. |
| 152 | Reinvite remembered users without retyping ID | NOT IMPLEMENTED | No trusted-user UI. |
| 153 | Remembering device never grants automatic access | NOT IMPLEMENTED | No trust/approval policy. |
| 154 | Joined/left/new Host/reconnecting notifications | NOT IMPLEMENTED | No session notifications. |
| 155 | Auto-dismiss session notifications without interrupting playback | NOT IMPLEMENTED | No session notification layer. |
| 156 | Timestamped collapsible activity log, collapsed by default | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |

## Session permissions, reconnect and compatibility

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 157 | Who Can Start/Stop: Host Only or Everyone | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 158 | Default to Host Only | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 159 | Everyone mode still requires authorized membership | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 160 | Host approves join requests | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 161 | Host changes session settings | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 162 | Member participates in synchronized playback | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 163 | Member leaves independently | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 164 | Member retains own settings/task while connected | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 165 | Permission structure extensible to future Co-Host | NOT IMPLEMENTED | No roles architecture. |
| 166 | Backend validates membership | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 167 | Backend validates current Host for restricted commands | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 168 | Backend validates device identity | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 169 | Backend validates session IDs | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 170 | Backend validates invite validity | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 171 | Backend validates command type/payload/state legality | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 172 | Enforce security beyond hiding UI controls | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 173 | Bounded reconnect grace window | NOT IMPLEMENTED | No connection lifecycle. |
| 174 | Restore member into same session | NOT IMPLEMENTED | No reconnect protocol. |
| 175 | Restore role consistently with host failover | NOT IMPLEMENTED | No role restoration. |
| 176 | Restore Ready where still appropriate | NOT IMPLEMENTED | No prepared-state reconnect validation. |
| 177 | Restore selected task display | NOT IMPLEMENTED | No task presence metadata. |
| 178 | Resynchronize session state after reconnect | NOT IMPLEMENTED | No snapshot/resync protocol. |
| 179 | Clean removal after reconnect timeout | NOT IMPLEMENTED | No timeout/removal. |
| 180 | Show each member's selected local task | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 181 | Each participant runs own local recording on session command | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 182 | Synchronize control/timing/Ready/state only; never upload macros automatically | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 183 | Version/protocol compatibility check before join | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 184 | Friendly block for incompatible peers | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 185 | Small Update Available indicator | NOT IMPLEMENTED | No updater/version polling. |
| 186 | Do not force upgrades except incompatible session protocol | NOT IMPLEMENTED | No update/session compatibility policy. |

## Session reliability and ordered events

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 187 | Idempotently handle duplicate invites | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 188 | Handle invalid public ID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 189 | Handle offline public ID | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 190 | Handle expired code | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 191 | Handle declined invitation | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 192 | Handle member disconnect without corrupting session | PARTIALLY IMPLEMENTED | SessionConnection detects loss and stops local input; SessionState removes disconnected member/elects host. No reconnect grace/state recovery. |
| 193 | Detect unexpected app closure in session | PARTIALLY IMPLEMENTED | SessionConnection detects loss and stops local input; SessionState removes disconnected member/elects host. No reconnect grace/state recovery. |
| 194 | Handle duplicate Start | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 195 | Handle duplicate Stop | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 196 | Handle session Start while running | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 197 | Handle session Stop while already stopped | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 198 | Unique session event/sequence numbers | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 199 | Reject duplicate sequence | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 200 | Reject old commands | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 201 | Reject out-of-order commands | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 202 | Reject wrong-session commands | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |
| 203 | Consistency through host transfer/repeated events | IMPLEMENTED | src/TinyTask.Diagnostics/SessionConnection.cs and SessionWindow.cs; src/TinyTask.SessionServer/SessionState.cs: Windows client / configured server implementation. tests/SessionChecks and tests/SessionClientChecks validate loopback protocol, identity, approvals, permissions and ordering; not public-hosting or Mac acceptance. |

## Synchronized starts and pre-synchronization

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 204 | Measure latency continuously after joining, before Start | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 205 | Continuously estimate clock offset | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 206 | Continuously measure jitter | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 207 | Continuously track connection stability | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 208 | Synchronize before Start instead of negotiating then | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 209 | Server chooses near-future execution timestamp | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 210 | Distribute same timestamp to every participant | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 211 | Host waits for same timestamp as guests | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 212 | Guests start by target time, not arrival time | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 213 | Dynamic millisecond-scale buffer based on quality | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 214 | Avoid automatic multi-second synchronized delay | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 215 | Poor-connection warning | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 216 | Healthy clients do not wait seconds for unstable peer | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 217 | Fast Start Mode | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 218 | Fast Start keeps clients synchronized/tasks preloaded | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 219 | Host selects Start Without Them or Cancel Start | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 220 | Default Fast Start to Start Without Them | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 221 | Notify excluded member | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 222 | Never silently start excluded/late member seconds afterward | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 223 | Start uses small fraction of roughly five-second response window | NOT IMPLEMENTED | No measured multi-device start latency. |
| 224 | Optional scheduled Start In 3 seconds | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 225 | Optional scheduled Start In 5 seconds | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 226 | Optional scheduled Start In 10 seconds | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 227 | Optional custom session start delay | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 228 | Synchronized visible countdown on all devices | NOT IMPLEMENTED | No distributed countdown. |
| 229 | Countdown only for intentional scheduled start | NOT IMPLEMENTED | No countdown policy. |
| 230 | Local playback remains independent of session buffer | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |

## Original timeline, scheduling, preparation and desync

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 231 | Original recorded delays unchanged during local playback | IMPLEMENTED | W snapshots actions; T reads only; M calculates separate offsets. |
| 232 | Derive original relative event timestamps | IMPLEMENTED | T constructor; M.play cumulative original delays. |
| 233 | Speed-adjusted offsets computed before clock starts | IMPLEMENTED | T/W.Play; M.play. |
| 234 | Each local event derives from authoritative start, not prior completion | IMPLEMENTED | T.Due/W.Play; M.tick. |
| 235 | Do not chain delays after actual completion | IMPLEMENTED | W/T/M fixed deadlines. |
| 236 | Slightly late event does not move next deadline | IMPLEMENTED | W/M dispatch overdue actions but keep offsets; TT assertion. |
| 237 | Preserve action order | IMPLEMENTED | W prepared snapshot sequential iteration; M index. |
| 238 | Do not silently skip important local events to catch up | IMPLEMENTED | W/M process each action; severe-backlog problem separately missing below. |
| 239 | Use high-resolution monotonic clock | IMPLEMENTED | W.Stopwatch; M.ProcessInfo.systemUptime. |
| 240 | Wall-clock/DST changes do not move local active deadlines | IMPLEMENTED | W/M use monotonic timing rather than DateTime/Date scheduling. |
| 241 | Loop target = original start + index × original duration | IMPLEMENTED | T.Due/W.Play; M.tick; TT long-loop check. |
| 242 | Do not rewrite mouse/key/loop timing because of lag | IMPLEMENTED | W/T/M preserve action delays. |
| 243 | Translate common session start to local monotonic time | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 244 | All peers reference same conceptual session timeline | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 245 | Measure expected versus actual position/drift in ms | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 246 | Ignore insignificant measured cross-device drift | NOT IMPLEMENTED | No thresholds/policy. |
| 247 | Moderate desync converges toward original timeline | NOT IMPLEMENTED | Local fixed deadlines exist; no cross-device correction. |
| 248 | Determine drift thresholds by testing | NOT IMPLEMENTED | No network/desync measurements. |
| 249 | Explicit severe-lateness classification/policy | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 250 | Prevent unsafe large overdue input backlog | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 251 | Mark severely late client Desynced | NOT IMPLEMENTED | No desync state. |
| 252 | Stop locally when lateness makes execution unsafe | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 253 | Resynchronize at safe end/start-of-loop boundary | NOT IMPLEMENTED | No recovery state machine. |
| 254 | Rejoin at next safe loop start | NOT IMPLEMENTED | No rejoin scheduling. |
| 255 | Warn about severe/unstable drift | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 256 | Compile when loaded or marked Ready | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 257 | Precompile type/key/button/coordinates/press-release/loop information | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 258 | Prepare native input events ahead of due time | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 259 | Keep multiple upcoming actions execution-ready | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 260 | Look-ahead queue of fully prepared actions | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 261 | Do not reparse JSON for every action | IMPLEMENTED | W/M parse before playback and iterate models. |
| 262 | Classify early/on-time/slightly-late/severely-late without retiming | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 263 | Efficient long wait then precise final interval | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 264 | Platform high-resolution final wait | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |
| 265 | Avoid excessive busy-wait CPU | IMPLEMENTED | W asynchronous delay/yield; M run-loop timer, no spin. |
| 266 | Ready requires loaded/parsed/native-prepared task and config | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 267 | Ready requires synced clock and armed start | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 268 | Preparing then Ready—Synced UI | PARTIALLY IMPLEMENTED | SessionConnection clock samples/offset/jitter; SessionState timestamped start, participant selection and optional schedule; SessionWindow + H/W prepared local playback. Loopback integration tested; multi-computer timing, Mac integration and full recovery remain unvalidated. |
| 269 | Start merely releases already prepared timeline | PARTIALLY IMPLEMENTED | W.Prepare/Play and src/TinyTask.Diagnostics/PrecisionWait.cs: native INPUT snapshots, absolute deadlines, high-resolution timer and conservative 500ms lateness stop; Windows tested. Mac preparation/desync work remains. |

## Nearby Sessions, privacy and routing

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 270 | Discover TinyTask on same LAN/Wi-Fi | NOT IMPLEMENTED | No discovery service. |
| 271 | Nearby means LAN, not GPS/location | NOT IMPLEMENTED | No Nearby feature; absence of GPS is not implementation. |
| 272 | Invite by ID / Join with Code / Nearby entry options | PARTIALLY IMPLEMENTED | SessionWindow offers Invite by ID and Join with Code; Nearby entry/discovery remains missing. |
| 273 | Nearby device name/username/public ID/ping/Invite | NOT IMPLEMENTED | No device list. |
| 274 | Nearby session members/lock state/Request to Join | NOT IMPLEMENTED | No session list. |
| 275 | Hide raw IP from normal Nearby UI | NOT IMPLEMENTED | No Nearby model/UI. |
| 276 | Standard LAN discovery independent of central server | NOT IMPLEMENTED | No mDNS or alternative discovery. |
| 277 | Minimal discovery metadata | NOT IMPLEMENTED | No advertisement schema. |
| 278 | Never broadcast macros/settings/unnecessary device info | NOT IMPLEMENTED | No discovery privacy enforcement. |
| 279 | Allow Nearby Discovery setting | NOT IMPLEMENTED | No preference. |
| 280 | Off stops advertising and hides availability | NOT IMPLEMENTED | No advertisement lifecycle. |
| 281 | Only While Session Screen Is Open mode | NOT IMPLEMENTED | No screen-scoped discovery. |
| 282 | Always While TinyTask Runs mode | NOT IMPLEMENTED | No background discovery. |
| 283 | Default to Only While Session Screen Is Open | NOT IMPLEMENTED | No discovery default. |
| 284 | Public ID/code still works with discovery Off | NOT IMPLEMENTED | Neither connection path exists. |
| 285 | Discovery never automatically connects | NOT IMPLEMENTED | No discovery/approval integration. |
| 286 | LAN invite needs sender confirmation and receiver Accept/Decline | NOT IMPLEMENTED | No LAN invitation flow. |
| 287 | Host chooses to advertise unlocked session | NOT IMPLEMENTED | No advertisement control. |
| 288 | Nearby join request still requires Host approval | NOT IMPLEMENTED | No LAN join authorization. |
| 289 | Locked sessions hidden or marked locked with joining disabled | NOT IMPLEMENTED | No discovery filtering. |
| 290 | Shared network never implies trust or bypasses validation | NOT IMPLEMENTED | No authenticated LAN protocol. |
| 291 | Trusted nearby device label without autoaccept | NOT IMPLEMENTED | No trust/discovery integration. |
| 292 | Refresh after Wi-Fi disconnection/network change | NOT IMPLEMENTED | No network-change handler. |
| 293 | Refresh after Ethernet/Wi-Fi switch/IP reassignment | NOT IMPLEMENTED | No adapter/address handling. |
| 294 | Refresh on sleep/wake and expire crashed peers | NOT IMPLEMENTED | No discovery expiry/resume. |
| 295 | Identify by permanent ID rather than local IP | NOT IMPLEMENTED | No identity/discovery mapping. |
| 296 | Measure LAN versus online quality | NOT IMPLEMENTED | No route comparison. |
| 297 | Prefer LAN only when lower/stabler and secure | NOT IMPLEMENTED | No selection policy. |
| 298 | Preserve identity/permissions on both routes | NOT IMPLEMENTED | No multi-transport authorization. |
| 299 | Optional Local/Online + ping + sync details | NOT IMPLEMENTED | No path indicator. |
| 300 | Fall back LAN→online without destroying session | NOT IMPLEMENTED | No transport fallback. |
| 301 | Switch back to LAN when safely advantageous | NOT IMPLEMENTED | No transport promotion. |
| 302 | Switch only at safe timing boundaries | NOT IMPLEMENTED | No handoff protocol. |

## Advanced input, devices and virtual focus

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 303 | One computer/no VM, macro and user independently control different apps | PARTIALLY IMPLEMENTED | R/DUAL/PROBE demonstrate limited paths, not complete usable isolation. |
| 304 | Background clicks/movement to chosen nonfocused window | PARTIALLY IMPLEMENTED | R/WP window-message code; WP not wired into H.PlayMacro; target-dependent. |
| 305 | Background keyboard/text to chosen window | PARTIALLY IMPLEMENTED | R.Key/Text and WP; diagnostic success is not complete keyboard support. |
| 306 | Keep real cursor and focus unchanged during background playback | PARTIALLY IMPLEMENTED | R/DUAL limited-target evidence; no full product guarantee. |
| 307 | Select target window/application | IMPLEMENTED | H.RefreshTargets; SW; D; NATIVE.Target validation. |
| 308 | Friendly target name/icon by default | IMPLEMENTED | H.FriendlyTarget template; SW. |
| 309 | HWND/PID/title under technical details | IMPLEMENTED | H.UpdateDetails/Technical expander; D. |
| 310 | Friendly currently focused app display | IMPLEMENTED | H.UpdateDetails. |
| 311 | Multiple macros run concurrently | NOT IMPLEMENTED | One H.engine; W.IsBusy prevents concurrent local playback. |
| 312 | Separate macro/target/state/loops for each app | NOT IMPLEMENTED | No multiple-player manager. |
| 313 | Graceful fallback when background input unsupported | PARTIALLY IMPLEMENTED | H disables Advanced playback/explains limits; R rejects invalid targets. No configurable usable fallback player. |
| 314 | Strict probes never silently switch to focus-stealing global input | IMPLEMENTED | R explicit paths; ROB separates foreground controls; H Advanced playback disabled. |
| 315 | Enumerate individual Windows mouse/keyboard devices | IMPLEMENTED | RAW.Devices/RawSample; D. |
| 316 | Identify/observe selected physical device's packets | IMPLEMENTED | RAW; SW.Observe filters handle. |
| 317 | Save separate macro/user mouse assignments | IMPLEMENTED | SW.Next; H.HomePreferences paths. |
| 318 | Friendly mouse names; raw HID IDs only in diagnostics | IMPLEMENTED | AS.MouseName; SW.MouseChoice; D. |
| 319 | Disconnected assignment does not silently select another mouse | IMPLEMENTED | H.UpdateDetails; SW.Next/restoration; UI tests. |
| 320 | Correct DPI/pointer-speed difference between cursors | PARTIALLY IMPLEMENTED | SW.PointFromScreen/MatchWindowsPointer/sensitivity tested; matching follows shared OS cursor, not independently calibrated input streams. |
| 321 | Retain fractional virtual pointer movement | IMPLEMENTED | SW.Observe doubles; TestCursorScaling. |
| 322 | Independent visible second cursor | PARTIALLY IMPLEMENTED | D.CursorWindow/SW/PROBE visual pointer works; not physical input isolation. |
| 323 | Clone OS cursor appearance and changing cursor states | NOT IMPLEMENTED | Current pointers are simple visuals. |
| 324 | Virtual cursor clicks applications | PARTIALLY IMPLEMENTED | R/PROBE controlled routing; no complete Advanced player. |
| 325 | Virtual cursor scrolls applications | PARTIALLY IMPLEMENTED | R.Scroll/BROKER experiments; not product integration. |
| 326 | Normal arbitrary-window interaction through second cursor | PARTIALLY IMPLEMENTED | PROBE/BROKER limited routing, not a general desktop interaction layer. |
| 327 | Separate real and virtual focus tracking | PARTIALLY IMPLEMENTED | PROBE/BROKER work target and WP receiver state; no complete virtual focus manager. |
| 328 | Route user's physical keyboard to work app while Roblox keeps real focus | PARTIALLY IMPLEMENTED | PROBE fixture experiment; Roblox guard stops on any key; H only explains limits. |
| 329 | Prevent user mouse/click leakage into Roblox while routing to work app | PARTIALLY IMPLEMENTED | PROBE legacy hook routing leaves possible raw leakage; DRV/POL not loaded/tested on hardware. |
| 330 | Released app fully isolates macro/user keyboard paths | NOT IMPLEMENTED | No installed keyboard filter or working released isolation backend. |
| 331 | Filter per-device mouse before Windows merges it | PARTIALLY IMPLEMENTED | DRV/POL unsigned development binary/source; no installed/hardware result. |
| 332 | Independent OS cursor/focus environments within desktop | NOT IMPLEMENTED | Device identity/overlay does not implement this. |
| 333 | Usable one-physical-mouse macro/user mode | PARTIALLY IMPLEMENTED | PROBE one-mouse bounded experiment; SW assignment flow still asks for two devices. |
| 334 | User on Task View desktop 1 while target runs on desktop 2 | PARTIALLY IMPLEMENTED | PROBE existing routing does not establish cross-virtual-desktop operation; visibility/foreground constraints remain. |
| 335 | Closing app restores normal input | PARTIALLY IMPLEMENTED | W/D cleanup and PROBE/BROKER/DRV owner/lease safeguards; installed-driver crash recovery untested. |
| 336 | Ctrl+Alt+F12 stops cursor/device observation | IMPLEMENTED | D.Initialize/Stop; SW.WndProc/StopObservation. |
| 337 | Emergency disable restores physical controls from real isolation | PARTIALLY IMPLEMENTED | PROBE/BROKER F10 and POL recovery paths; no installed isolation acceptance. |
| 338 | Clearly mark unsupported Advanced features experimental | IMPLEMENTED | H/AS/D labels; U Advanced explanation. |
| 339 | Advanced faults cannot break Classic | PARTIALLY IMPLEMENTED | R/WP/AS/driver modularity and separate builds; same-process diagnostics not comprehensively fault-isolated. |

## Main UI, wizard and preserved diagnostics

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 340 | Compact TinyTask 2.0 window with Classic/Advanced selector | IMPLEMENTED | H.Layout/ModeChanged; U launch UI. |
| 341 | Open/Save/Record/Play/Pause/Stop/Preferences toolbar | IMPLEMENTED | H.Layout/handlers; U actions. |
| 342 | Macro name, speed, loops and Ready/Recording/Playing/Paused status | IMPLEMENTED | H.EngineChanged/Layout; U.update. |
| 343 | Classic hides technical window/device details and logs | IMPLEMENTED | H.AdvancedPanel collapsed; U Classic view. |
| 344 | Rounded controls/clear spacing/consistent typography and toolbar sizes | IMPLEMENTED | TH/H.Layout; U native rounded controls. |
| 345 | Windows light and dark modes | IMPLEMENTED | TH.Apply/Styles; H.Theme; UI snapshots. |
| 346 | macOS light/dark/system appearance | IMPLEMENTED | U.applyAppearance/preferences; MB smoke tests in earlier CI. |
| 347 | Consistent dark appearance for every dialog/popup/hover/disabled state | PARTIALLY IMPLEMENTED | TH custom styling/U native appearance; exhaustive OS-dialog/all-state validation absent. |
| 348 | Readable Advanced selector in dark mode | IMPLEMENTED | TH ComboBox style; UI snapshots. |
| 349 | Settings → Advanced → Diagnostics retains Input Lab | IMPLEMENTED | H.CreateSettingsWindow/OpenDiagnostics; D. |
| 350 | Guided setup on first Advanced use | IMPLEMENTED | H.ModeChanged/SetupSeen; SW. |
| 351 | Wizard step 1: friendly target names/icons | IMPLEMENTED | SW step 0; H.FriendlyTarget. |
| 352 | Wizard step 2: normal mouse or second mouse | IMPLEMENTED | SW setup branches. |
| 353 | Wizard step 3: assign mouse for TinyTask and for user | IMPLEMENTED | SW step 2/Next. |
| 354 | Wizard step 4: selected movement detection and safe click test | IMPLEMENTED | SW.Observe/BuildTestPad/UpdateFeedback. |
| 355 | Test button repeatedly registers clicks with correct hit area | IMPLEMENTED | SW.TestRepeatedClicks; UI; tests/timeline-dev/ui.json. |
| 356 | Wizard step 5 tests target click/key/background/isolation compatibility | PARTIALLY IMPLEMENTED | SW shows historical Roblox results or Not tested; no actual target test on this page. |
| 357 | Wizard avoids fake compatibility success | IMPLEMENTED | SW explicitly says no target input sent and unavailable features. |
| 358 | Advanced feature rows have setup/choose/test/configure actions | IMPLEMENTED | H.InputSupport/ChooseMouse/TestCursor/KeyboardSetup wiring. |
| 359 | Live meaningful installed/tested isolation status | PARTIALLY IMPLEMENTED | AS static blocker status is honest but stale relative to local driver adapter; no live capability detector. |
| 360 | Keyboard Configure opens working setup | PARTIALLY IMPLEMENTED | H.KeyboardSetup opens explanation only. |
| 361 | Diagnostics raw device list/IDs | IMPLEMENTED | D.RefreshDevices; RAW. |
| 362 | Diagnostics HWND/PID/window/focus details | IMPLEMENTED | D/NATIVE. |
| 363 | Diagnostics packet counts/source observation | IMPLEMENTED | D.WndProc/Tick; RAW. |
| 364 | Diagnostics cursor positions/routing status | IMPLEMENTED | D.Tick/Track/MoveVirtual. |
| 365 | Diagnostics Test Click | IMPLEMENTED | D.Probe; R.Click. |
| 366 | Diagnostics keyboard down/up/text | IMPLEMENTED | D.Probe; R.Key/Text. |
| 367 | Diagnostics Test Scroll | IMPLEMENTED | D.Probe; R.Scroll. |
| 368 | Diagnostics UI Automation probe | IMPLEMENTED | D.Probe; R.Invoke/UiaWorker. |
| 369 | Diagnostics logs and manual worked/failed verdict | IMPLEMENTED | D.Write/Verdict. |
| 370 | Diagnostics export report | IMPLEMENTED | D.Export. |
| 371 | Diagnostics emergency release | IMPLEMENTED | D.Stop/R.ReleaseAll. |
| 372 | Friendly empty Play message in normal Classic app | IMPLEMENTED | H.PlayMacro/TestEmptyPlay; U.playPause/UI smoke. |
| 373 | Same empty Play behavior in every requested playback context | PARTIALLY IMPLEMENTED | H/U Classic implemented; Advanced/session players do not exist. |

## Advanced components, virtual HID and recovery

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 374 | Investigate user-mode first before drivers | IMPLEMENTED | RESEARCH/VHF and R/PROBE progression. |
| 375 | Research Raw Input/HID/class merging/messages/UIA/focus restrictions | IMPLEMENTED | RESEARCH/VHF; ROB/PROBE experiments. |
| 376 | Research virtual mouse/keyboard, VHF/UMDF/KMDF, reports/signing/install | IMPLEMENTED | VHF/FIX; DRV work. |
| 377 | Investigate macOS input separation/accessibility/device routing | PARTIALLY IMPLEMENTED | RESEARCH/ML preliminary work; no tested isolated Mac architecture. |
| 378 | Investigate remote input/multipointer/virtual focus alternatives | PARTIALLY IMPLEMENTED | RESEARCH/PROBE discussion and experiments; not comprehensive validation of alternatives. |
| 379 | Own minimal Microsoft-supported mouse component | PARTIALLY IMPLEMENTED | DRV/POL/BROKER development filter, not virtual HID source; unsigned/uninstalled. |
| 380 | Create real virtual mouse for TinyTask | NOT IMPLEMENTED | No approved installed virtual HID mouse. |
| 381 | Create real virtual keyboard for TinyTask | NOT IMPLEMENTED | No approved installed virtual HID keyboard. |
| 382 | Device-backed Move/LeftDown/LeftUp/EscapeDown/EscapeUp test controls | NOT IMPLEMENTED | No functioning virtual-device test UI. |
| 383 | Live virtual device installed/connected state | NOT IMPLEMENTED | AS general Raw Input counts do not establish managed virtual-device status. |
| 384 | Trusted signed redistributable Advanced component candidate | PARTIALLY IMPLEMENTED | VHF licensed package inspected; FIX alternative source work; none approved for automatic distribution. |
| 385 | Do not bundle vendor-licensed HID without rights | IMPLEMENTED | AS blocks setup; VHF licensing evidence; no such release integration. |
| 386 | Never silently install drivers | IMPLEMENTED | AS disabled install; development DRV not deployed. |
| 387 | Do not download/install unsigned end-user drivers | IMPLEMENTED | AS has no driver downloader; unsigned own binary stays development-only. |
| 388 | Do not disable signature enforcement/security | IMPLEMENTED | AS/DRV build path does not bypass/install; signing gate remains. |
| 389 | Offer optional dependency setup information on launch | PARTIALLY IMPLEMENTED | D.ShowSetup first-run information; H AdvancedSupport, not functioning automatic dependency setup. |
| 390 | Settings → Advanced → Install Advanced Input Support entry | IMPLEMENTED | H.CreateSettingsWindow calls AS.Create; entry exists, install does not. |
| 391 | Explain concrete component before install | PARTIALLY IMPLEMENTED | AS explains blockers; no approved concrete package/install flow. |
| 392 | Ask confirmation for concrete install | NOT IMPLEMENTED | Install disabled. |
| 393 | Download approved component from trusted source | NOT IMPLEMENTED | No downloader. |
| 394 | Verify integrity hash before install | NOT IMPLEMENTED | Research reports are not runtime verification pipeline. |
| 395 | Verify publisher/signature/catalog before install | NOT IMPLEMENTED | No runtime package verifier. |
| 396 | Enforce approved license/source policy during setup | NOT IMPLEMENTED | No package manifest/policy mechanism beyond disabling installs. |
| 397 | Request administrator permission when necessary | NOT IMPLEMENTED | No driver elevation helper. |
| 398 | Automatically install/configure component | NOT IMPLEMENTED | AS Install disabled. |
| 399 | Verify virtual mouse appears after install | NOT IMPLEMENTED | No install/device identity check. |
| 400 | Verify virtual keyboard appears after install | NOT IMPLEMENTED | No install/device identity check. |
| 401 | Run compatibility test after install | NOT IMPLEMENTED | No installed-device workflow. |
| 402 | Show Ready only after verified setup | NOT IMPLEMENTED | No readiness state machine. |
| 403 | Failed installation with Retry/Details | NOT IMPLEMENTED | No attempted-install lifecycle. |
| 404 | Standard installation works immediately without Advanced dependency | IMPLEMENTED | SET self-contained install; H Classic independent of AS. |
| 405 | Advanced Installation choice configures input components | NOT IMPLEMENTED | SET has no component-install branch. |
| 406 | Standard users can complete one-click Advanced setup later | NOT IMPLEMENTED | AS entry exists but disabled installation. |
| 407 | Uninstall managed component from Settings | NOT IMPLEMENTED | AS says none installed; no removal workflow. |
| 408 | Classic remains working after component uninstall | NOT IMPLEMENTED | Classic independent, but actual uninstall behavior cannot be verified because no component lifecycle exists. |
| 409 | Failed component install cannot harm normal mouse/keyboard | PARTIALLY IMPLEMENTED | POL/DRV fail-open design/lease tests; actual rollback and installation safety untested. |
| 410 | Test physical mouse after install | NOT IMPLEMENTED | No installed-device test. |
| 411 | Test physical keyboard after install | NOT IMPLEMENTED | No installed-device test. |
| 412 | Test emergency stop with installed component | NOT IMPLEMENTED | Software prototypes only. |
| 413 | Verify Advanced Mode detects installed devices | NOT IMPLEMENTED | No approved device integration. |
| 414 | Clean driver install/uninstall/failure rollback | NOT IMPLEMENTED | No distributable INF/catalog/install/recovery package. |
| 415 | Avoid reboot where possible and measure actual requirement | NOT IMPLEMENTED | No install/removal measurement. |
| 416 | Recover on close/crash/device disconnect at low-level input layer | PARTIALLY IMPLEMENTED | POL/DRV/BROKER owner/lease paths and software tests; loaded-driver recovery untested. |

## Roblox and application compatibility tests

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 417 | Detect RobloxPlayerBeta process/PID/HWND/visible window | IMPLEMENTED | ROB.Run; NATIVE.Windows/Target; PROBE. |
| 418 | Log foreground/focus before and after | IMPLEMENTED | ROB/PROBE reports. |
| 419 | Reject invalid focus setup instead of counting as Roblox failure | IMPLEMENTED | ROB validity/preconditions; DUAL fixture setup. |
| 420 | Separate message-path tests from injected controls | IMPLEMENTED | ROB method switches/reports. |
| 421 | Harmless menu/Escape only; no gameplay/chat/trades/purchases | IMPLEMENTED | ROB test scope; docs/ROBLOX-2026-09-20.md. |
| 422 | Investigate beyond first failed API | IMPLEMENTED | ROB post/send/UIA/scan control paths; VHF/PROBE. |
| 423 | Log method/target IDs/delivery/focus/reaction/validity | IMPLEMENTED | ROB reports, screenshots/manual verdict distinction. |
| 424 | Roblox test through truly isolated input path | PARTIALLY IMPLEMENTED | PROBE guard exists; no verified isolation and raw leakage unresolved. |
| 425 | Virtual-HID mouse/Escape test with Roblox foreground | NOT IMPLEMENTED | No installed virtual HID prototype. |
| 426 | Virtual-HID mouse/Escape test with Roblox background | NOT IMPLEMENTED | No installed virtual HID prototype. |
| 427 | Measure report acceptance, Roblox reaction, cursor/focus and physical independence | NOT IMPLEMENTED | No device-backed Roblox test matrix. |
| 428 | Temporary app-specific blocker prevents physical mouse reaching Roblox | PARTIALLY IMPLEMENTED | PROBE suppresses/routes legacy input; raw input may still reach Roblox; DRV not deployed. |
| 429 | Roblox keyboard test no longer cancels on every key | PARTIALLY IMPLEMENTED | PROBE native fixture routes keys; Roblox guard still cancels on any key. |
| 430 | Observe user tests without video upload | PARTIALLY IMPLEMENTED | PROBE telemetry and screenshots; no reliable current live-observation workflow. |
| 431 | Do not equate successful install/API call with Roblox compatibility | IMPLEMENTED | AS/SW/VHF/ROB explicit limitations. |
| 432 | No Roblox modification/injection/anti-cheat bypass | IMPLEMENTED | ROB/R/PROBE documented OS input; DRV input-stack code, not Roblox tampering. |
| 433 | Controlled native-app background tests | IMPLEMENTED | DUAL; tests/dual-native.json. |
| 434 | Controlled browser background tests | IMPLEMENTED | BROWSER/DUAL; tests/dual-browser.json. |
| 435 | Multiple target-window compatibility | PARTIALLY IMPLEMENTED | DUAL/earlier reports; simultaneous-click failures and no concurrent macro player. |
| 436 | Real-time-rendered/game background mouse compatibility | PARTIALLY IMPLEMENTED | ROB/PROBE investigation, not general support. |
| 437 | Physical-device separation/leakage data collection | PARTIALLY IMPLEMENTED | RAW/SW/PROBE evidence; no completed hardware two-device isolation acceptance. |

## Desktop delivery, product identity and release

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 438 | Reliable Windows C#/.NET/WPF stack with native APIs | IMPLEMENTED | src/TinyTask.Diagnostics/TinyTask.Diagnostics.csproj; W/NATIVE/RAW/R. |
| 439 | Native Mac CGEvent/Quartz/Accessibility/Input Monitoring | IMPLEMENTED | M/U/ML; MB. |
| 440 | Shared cross-platform core wherever practical | PARTIALLY IMPLEMENTED | W/T and M parallel implementations, not a shared executable core. |
| 441 | Windows standalone EXE, no terminal/IDE/manual runtime | IMPLEMENTED | BUILD self-contained publish; existing release binaries. |
| 442 | Windows installer | IMPLEMENTED | SET/BUILD; TinyTask2-Windows.Setup.exe. |
| 443 | Correct Start Menu shortcut | IMPLEMENTED | SET/ID; LAUNCH. |
| 444 | Correct desktop shortcut | IMPLEMENTED | SET/ID; LAUNCH. |
| 445 | App icon/version metadata | IMPLEMENTED | app.ico/csproj; Mac plists/MB icon generation. |
| 446 | Separate normal-user and Windows Input Lab builds | IMPLEMENTED | ID conditional product identities; BUILD. |
| 447 | Proper TinyTask 2.0.app | IMPLEMENTED | MB/User-Info.plist; prior release ZIP. |
| 448 | Separate TinyTask 2.0 Input Lab.app | IMPLEMENTED | MB/ML/Info.plist; prior release ZIP. |
| 449 | Intel/Apple Silicon Mac app packaging | IMPLEMENTED | MB arm64/x86_64 and lipo. |
| 450 | Downloaded Mac app opens normally without security workarounds | PARTIALLY IMPLEMENTED | MB ad-hoc signed; Developer ID/notarization incomplete. |
| 451 | Mac users need no Python/Node/Xcode/Terminal | IMPLEMENTED | Native U/M bundle; tools only needed for development. |
| 452 | Mac app requests control/monitoring permission | IMPLEMENTED | U.permissions: AXIsProcessTrustedWithOptions, CGRequestListen/PostEventAccess. |
| 453 | Mac user never needs manual System Settings approval | PARTIALLY IMPLEMENTED | U requests/guides permission; cannot grant protected consent or guarantee no Settings step. |
| 454 | Explain/recheck missing or denied Mac permissions | IMPLEMENTED | U.refreshPermissions/prepareInput/applicationDidBecomeActive; M.enableMonitor. |
| 455 | Normal close releases hooks/taps/tray resources | IMPLEMENTED | H.Closed/W.Dispose; D.Cleanup; U termination/M.shutdown. |
| 456 | Fast startup/low CPU/lightweight/fast playback at production scale | PARTIALLY IMPLEMENTED | D.LifecycleTest and bounded polling; no full startup/session/high-speed performance acceptance. |
| 457 | Unique EXE names/app IDs/product GUIDs | IMPLEMENTED | ID/SET do not reuse original TinyTask identifiers. |
| 458 | Absolute self/helper paths; no generic TinyTask.exe lookup | IMPLEMENTED | ID.ExecutablePath; SET; ROB helper paths. |
| 459 | Separate uninstall/installer identity | IMPLEMENTED | SET.RegistryPath; ID.ProductGuid. |
| 460 | Original TinyTask untouched and coexists | IMPLEMENTED | LAUNCH/saved coexistence reports; ID/SET. |
| 461 | Preserve normal/tester local outputs during new releases | IMPLEMENTED | BUILD separate paths; dist/timeline-dev and preserved rc3 outputs. |
| 462 | Publish to existing GitHub repo with clear release tag | IMPLEMENTED | README/REL/PUB: WilliamO2025/tinytask2-builds, v0.2.0-rc3; historical evidence. |
| 463 | Public repository/download access | IMPLEMENTED | PUB anonymous asset checks from release pass, not freshly rechecked. |
| 464 | Clean portable/installer/Input Lab Windows release files | IMPLEMENTED | PUB and release/v0.2.0-rc3; .Setup.exe suffix retained for installer dispatch. |
| 465 | Clean normal/Input Lab macOS ZIP release files | IMPLEMENTED | PUB and release/v0.2.0-rc3. |
| 466 | Release notes distinguish Classic versus experimental Advanced/Roblox | IMPLEMENTED | REL/release notes/README. |
| 467 | Mac unzip→Applications→open→permission instructions | IMPLEMENTED | README/REL. |
| 468 | State incomplete signing/notarization honestly | IMPLEMENTED | README/REL/MB. |
| 469 | README Downloads and Windows/Mac installation instructions | IMPLEMENTED | README. |
| 470 | Fix/verify previously broken release download paths/content | IMPLEMENTED | PUB saved HTTP/digest checks; no fresh availability claim today. |
| 471 | Publish latest timing changes as tested release | NOT IMPLEMENTED | Local source changes not in public rc3. |
| 472 | Complete Developer ID signing and notarization | NOT IMPLEMENTED | MB defaults to ad-hoc; no completed notarization. |

## Engineering process and validation

| # | Requirement | Status | Actual code / limitation |
|---:|---|---|---|
| 473 | Continue current architecture; do not restart application | IMPLEMENTED | Existing W/H/M preserved; T incremental helper. |
| 474 | Preserve diagnostic functionality during UI simplification | IMPLEMENTED | D/R/RAW/SW retained; H.OpenDiagnostics. |
| 475 | Use Ponytail for implementation simplicity | IMPLEMENTED | Recorded skill usage; T uses standard library with no added dependency. |
| 476 | Focused CodeRabbit reviews for important changes | IMPLEMENTED | REVIEW and historical tests/coderabbit-*.ndjson. |
| 477 | Resolve important review issues before finalizing affected work | PARTIALLY IMPLEMENTED | Latest REVIEW has 0 issues; prior DRV optional-control-child fail-open issue remains, driver unfinished. |
| 478 | Windows build after latest timing change | IMPLEMENTED | dist/timeline-dev; preceding build 0 warnings/errors. |
| 479 | Mac build after latest timing change | NOT IMPLEMENTED | Earlier Mac release exists; latest M/U edits unbuilt. |
| 480 | Run existing Windows UI suite after fixes | IMPLEMENTED | UI; tests/timeline-dev/ui.json, 15 passed. |
| 481 | Run existing Windows Classic suite after fixes | IMPLEMENTED | CT; tests/timeline-dev/classic.json, 9 passed. |
| 482 | Verify original-timeline math with deterministic checks | IMPLEMENTED | TT, 13 passed in preceding turn. |
| 483 | Windows light-mode UI validation | IMPLEMENTED | UI snapshots and prior visual evidence. |
| 484 | Windows dark/settings/wizard/diagnostics validation | IMPLEMENTED | UI screenshots; latest dark main window visually inspected. |
| 485 | Mac light/dark tests after latest edits | PARTIALLY IMPLEMENTED | MB/U smoke checks ran in prior CI, not after latest changes. |
| 486 | Repeated Test button verification | IMPLEMENTED | SW.TestRepeatedClicks/UI, handler and controlled input. |
| 487 | Empty Play friendly-message verification | IMPLEMENTED | UI/H.TestEmptyPlay; U smoke code; latest Windows pass. |
| 488 | Loaded/recorded Play works after timing changes | IMPLEMENTED | CT fixture round-trip/2-loop check. |
| 489 | Pause/held-key cleanup/Stop/F10 checks | IMPLEMENTED | CT passed for latest Windows build. |
| 490 | Full hardware/permissions/crash/hotplug/multimonitor matrix on both OSes | PARTIALLY IMPLEMENTED | D/ROB/PROBE/CT subsets only; complete matrix absent. |
| 491 | Document attempts, observations, failures, explanations and alternatives | PARTIALLY IMPLEMENTED | RESEARCH/VHF/Roblox docs and tests; some summary/status text stale versus local source. |
| 492 | Distinguish actual tested behavior from prototype claims | IMPLEMENTED | AS/SW/README explicit limits; audit keeps partial statuses. |

## Specification provenance and exclusions

The five latest attachments are:

- `32852fca-58d7-4232-8104-d22037436390`: 52-section master specification.
- `8c036447-4b46-4e9c-8f8b-36842e8e957a` and `bfd2f981-947c-45f9-810a-ed9db6180ed5`: identical low-latency/LAN supplements.
- `56c2764a-1f64-4b31-b876-4fb9ea6530ec`: immutable original timeline correction.
- `06b40a7d-1971-49a6-99e7-7bd8ec4a6d83`: prepare actions before execution correction.

Earlier attached specifications audited: `806c8cf2`, `79f66f7e`, `30038bd5`, `0b714f9f`, `6a95e0b9`, and `4306ea87` (full identifiers remain in this conversation). Inline requirements additionally cover delivery, UI polish, GitHub publishing, dependency setup/security, one-computer/no-VM routing, DPI alignment, Roblox guard and physical-device tests.

Repeated requests to resume or publish, confirmations, suggested example usernames/codes, and hypothetical questions are not counted as additional duplicate features. The earlier instruction to delay full UI development until a HID experiment was superseded by the explicit later instruction to ship Classic without waiting for Advanced. No feature is credited merely because its name appears in a document.

## Totals

**Total requirements: 492**  
**Implemented: 241**  
**Partial: 128**  
**Missing: 123**

Totals count the numbered, deduplicated requirements above, including requested security constraints and validation/delivery work. They are not a percentage-complete or production-readiness claim. Implemented includes work completed before the latest timing increment.

| Checklist section | Implemented | Partial | Missing | Total |
|---|---:|---:|---:|---:|
| Recording and macro management | 18 | 4 | 8 | 30 |
| Playback, speed, loops and task profiles | 20 | 10 | 1 | 31 |
| Shortcuts and preferences | 10 | 13 | 14 | 37 |
| Account-free identity | 11 | 3 | 2 | 16 |
| Invitations, codes and membership | 29 | 3 | 10 | 42 |
| Session permissions, reconnect and compatibility | 18 | 2 | 10 | 30 |
| Session reliability and ordered events | 15 | 2 | 0 | 17 |
| Synchronized starts and pre-synchronization | 0 | 24 | 3 | 27 |
| Original timeline, scheduling, preparation and desync | 14 | 19 | 6 | 39 |
| Nearby Sessions, privacy and routing | 0 | 1 | 32 | 33 |
| Advanced input, devices and virtual focus | 13 | 19 | 5 | 37 |
| Main UI, wizard and preserved diagnostics | 29 | 5 | 0 | 34 |
| Advanced components, virtual HID and recovery | 9 | 8 | 26 | 43 |
| Roblox and application compatibility tests | 11 | 7 | 3 | 21 |
| Desktop delivery, product identity and release | 29 | 4 | 2 | 35 |
| Engineering process and validation | 15 | 4 | 1 | 20 |
