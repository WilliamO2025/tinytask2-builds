# Mouse filter policy prototype — NOT an installable driver

This is executable, tested packet-routing policy for a future per-device filter.
It does not register a device, hook Windows, install a service, or block Roblox.
There is no `.sys`, INF, installer, or signed package in this directory.

The selected device's packets enter a bounded queue for a private routing service;
all other devices pass through. Only the owning session can read/renew its lease.
A 500 ms lease, full queue, disconnect, session close, explicit stop, or clock rollback
restores pass-through. Arming requires both physical and Windows mouse buttons to be
released. Tests cover packet contents, selection, ownership and recovery.

This is not a per-application raw-input firewall: suppressing the selected physical
device would remove it from the normal Windows stream. The service must separately
deliver it to a compatible work application. Arbitrary work apps and separate OS
focus are not solved by this policy. Roblox acceptance remains unverified.

## Required before any hardware installation

* Implement a KMDF per-device adapter based on documented mouse filter callbacks.
  Serialize policy operations, retain complete MOUSE_INPUT_DATA packets, and obey
  partial-consumption rules when forwarding to the original class callback.
* Implement an authenticated, administrator-controlled service endpoint with actual
  PnP device identity, bounded reads, foreground validation, emergency stop and
  release of held inputs in the destination app on every stop or failure.
* Add a kernel timer for proactive lease expiry; this policy also checks on access.
  Session IDs are correlation values, not security credentials.
* Build with WDK, test PnP/power/removal/recovery and USB/Bluetooth hardware, then
  obtain Microsoft-trusted signing. No test-signing or protection downgrade.
* Validate license, publisher, signed catalog, install rollback and uninstall before
  enabling installation in TinyTask. Current machine lacks WDK and signing credentials.

Microsoft references:
- https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/keyboard-and-mouse-hid-client-drivers
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-requirements--windows-vista-and-later-

Maintainer check: compile `test_policy.cpp` with a C++17 compiler and run it.
No administrator privileges or drivers are required for the policy check.
