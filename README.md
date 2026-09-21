# TinyTask 2.0 private build workspace

Source mirror of the existing TinyTask 2.0 project. Native Windows Classic recorder and separate Input Lab; native macOS Classic app and experimental Mac Input Lab. No third-party HID driver dependency.

Windows maintainer build: `tools/build.ps1 -Publish` with .NET 8 SDK. End users open the self-contained EXEs or per-user installers.

Mac maintainer build: `bash macos/build.sh`. GitHub Actions builds universal macOS 13+ app bundles and runs model checks. Artifacts are development builds with ad-hoc signatures, not notarized public releases. Physical input and permission acceptance tests remain required.

Recordings contain input and may contain private text. None are included in this source repository. Macro JSON is platform-specific; original TinyTask binary .rec files are not supported.
