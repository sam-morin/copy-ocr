# Copy OCR

Click the icon in the menu bar (macOS) or notification area (Windows), drag over part of the screen,
and the text in that area is copied to your clipboard. OCR is done on-device by the OS
(Apple Vision on macOS, Windows.Media.Ocr on Windows). No network, no dependencies.

- **Left-click** the icon → select an area (Esc cancels)
- **Right-click** the icon → Capture Text / Launch at Login / Quit

## macOS (13 Ventura or later)

```sh
./mac/build.sh
cp -R "mac/build/Copy OCR.app" /Applications/
open "/Applications/Copy OCR.app"
```

First run: macOS asks for **Screen Recording** permission (System Settings → Privacy & Security →
Screen & System Audio Recording). Enable Copy OCR, then quit and reopen it.
Tip: while selecting, press Space to capture a whole window instead.

The icon flashes ✓ when text was copied, ✗ when none was found.

## Windows (10 1809 or later)

A prebuilt standalone exe is at `windows/dist/CopyOCR.exe` (no install or .NET needed) —
copy it anywhere and run it. To rebuild on Windows: `windows\build.ps1` (needs the .NET 8 SDK).
To build it from a Mac, add `-p:EnableWindowsTargeting=true`.

If Windows hides the icon, drag it from the `^` overflow onto the taskbar so it's one click away.
OCR uses your Windows display languages; add more under Settings → Time & Language → Language.
