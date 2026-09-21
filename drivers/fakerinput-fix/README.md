# FakerInput report-handling hardening

Research source patch, **not an installable driver or a Roblox compatibility fix**.
Based on official Ryochan7/FakerInput v0.1.1 `Queue.c`, distributed under the
included MIT license. `upstream/Queue.c` preserves the original; `Queue.c` is
the patched version; `hardening.patch` contains the difference.

## Fixed in source

- Validate the control packet before subtracting its header length or reading data.
- Validate outer/inner report IDs and declared lengths against the HID wire layouts.
- Check output-buffer retrieval before copying; complete a dequeued failed read once.
- Copy only the report bytes, return the actual byte count rather than buffer capacity,
  and omit the absolute-mouse structure's ABI padding from the wire report.
- Check version input and feature output lengths before accessing structures.
- Explicitly initialize feature response padding before returning it.

Microsoft documents that callers must check the status of
[WdfRequestRetrieveOutputBuffer](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdfrequest/nf-wdfrequest-wdfrequestretrieveoutputbuffer).
Its returned buffer capacity is not the number of bytes a driver actually transfers.

## Reproduction and validation

From a developer PowerShell with Visual Studio MSVC C++ tools installed:

```powershell
./test.ps1 -Baseline
./test.ps1
```

The harness extracts the actual report functions and supplies WDF request doubles.
The baseline passes **3 defect-reproduction checks**: copy attempted after failed
buffer retrieval, capacity returned as transferred length, and a feature write beyond
the declared buffer length. A baseline pass confirms those defects were reproduced;
it does not mean the original code is safe. Checked copy stubs prevent an unsafe
memory copy in the test process.

The patched routines pass **22 checks**, including malformed packets, failed/short
output buffers, queue failure, all four report layouts, absolute-mouse padding,
version handling, feature padding, and completion byte counts. MSVC builds with
warnings treated as errors. This is a user-mode unit harness, not a complete WDK
driver build, binary exploit demonstration, real-device test, or security audit.
CodeRabbit review evidence is retained in the project's local tests directory.

## Why this is not enabled in the application

The official archived package was inspected as data; it was not installed. Valid
vendor signatures on the original MSI/DLL/catalog do not cover our changed source
or a rebuilt binary. A rebuilt package needs appropriate trusted signing plus real
installation, device I/O, crash, reboot, physical-input, emergency-stop and uninstall
acceptance tests before it can be offered by the application. No signing account or
approved rebuilt package is currently available. Do not disable signature enforcement
or import a test root certificate to work around this.

Even a signed virtual HID driver only produces input reports. It does not itself
create independent Windows foreground focus, prevent physical input reaching Roblox,
or route HID reports to an arbitrary background window. Those are separate, unresolved
input-routing requirements. No new Roblox compatibility test has been performed with
this patch, and no Roblox background compatibility claim is made.

The normal Classic application and release artifacts remain unchanged. Automatic
Advanced installation remains disabled until an eligible package and its routing
behavior have been validated.

Upstream: https://github.com/Ryochan7/FakerInput/tree/v0.1.1

Microsoft's own source starting points remain preferable for a maintained component:
[VHF](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-)
and [UMDF vhidmini2](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/vhidmini2).
They are source frameworks/samples, not ready-made signed input-isolation packages.
Signing requirements depend on the chosen driver model; VHF kernel-driver requirements
should not be assumed to describe every UMDF deployment.
