#!/bin/bash
# Builds "Copy OCR.app" (universal: Apple Silicon + Intel) into mac/build/.
set -euo pipefail
cd "$(dirname "$0")"

APP="build/Copy OCR.app"
rm -rf build
mkdir -p "$APP/Contents/MacOS" build/obj

for arch in arm64 x86_64; do
  swiftc -O -target "$arch-apple-macos13.0" Sources/*.swift -o "build/obj/CopyOCR-$arch"
done
lipo -create build/obj/CopyOCR-* -output "$APP/Contents/MacOS/CopyOCR"
cp Info.plist "$APP/Contents/Info.plist"

# Ad-hoc sign so macOS can remember the Screen Recording permission.
codesign --force --sign - "$APP"

echo "Built: $(pwd)/$APP"
echo "Install: cp -R \"$APP\" /Applications/"
