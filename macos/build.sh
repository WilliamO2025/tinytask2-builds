#!/bin/bash
# Maintainer/CI build. End users open the .app; no scripts or runtime required.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/../dist/macOS"
mkdir -p "$OUT/build" "$OUT/AppIcon.iconset"
xcrun swift "$ROOT/make-icon.swift" "$OUT/build/icon.png"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$OUT/build/icon.png" --out "$OUT/AppIcon.iconset/icon_${size}x${size}.png" >/dev/null
  sips -z "$((size * 2))" "$((size * 2))" "$OUT/build/icon.png" --out "$OUT/AppIcon.iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$OUT/AppIcon.iconset" -o "$OUT/build/AppIcon.icns"
build_app() {
  local name="$1" executable="$2" plist="$3"
  shift 3
  local app="$OUT/$name.app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  for arch in arm64 x86_64; do
    xcrun swiftc -swift-version 5 -O -target "$arch-apple-macos13.0" -framework AppKit -framework ApplicationServices -framework IOKit "$@" -o "$OUT/build/$executable-$arch"
  done
  lipo -create "$OUT/build/$executable-arm64" "$OUT/build/$executable-x86_64" -output "$app/Contents/MacOS/$executable"
  cp "$ROOT/$plist" "$app/Contents/Info.plist"
  cp "$OUT/build/AppIcon.icns" "$app/Contents/Resources/AppIcon.icns"
  codesign --force --options runtime --sign "${SIGNING_IDENTITY:--}" "$app"
  codesign --verify --strict "$app"
  ditto -c -k --sequesterRsrc --keepParent "$app" "$OUT/$name.zip"
}
build_app "TinyTask 2.0" TinyTask2 User-Info.plist "$ROOT/ClassicEngine.swift" "$ROOT/TinyTask.swift"
"$OUT/TinyTask 2.0.app/Contents/MacOS/TinyTask2" --self-test > "$OUT/classic-self-test.json"
"$OUT/TinyTask 2.0.app/Contents/MacOS/TinyTask2" --ui-smoke "$OUT/user-ui-light.png" light
"$OUT/TinyTask 2.0.app/Contents/MacOS/TinyTask2" --ui-smoke "$OUT/user-ui-dark.png" dark
if [[ "${BUILD_INPUT_LAB:-1}" == "1" ]]; then
  build_app "TinyTask 2.0 Input Lab" TinyTaskInputLab Info.plist "$ROOT/InputLab.swift"
fi
echo "Built native universal apps in $OUT. Hardware/permission testing remains necessary."
echo "Ad-hoc signatures are development-only. Developer ID signing and notarization are required for a normal public download."
