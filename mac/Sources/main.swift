// Copy OCR — menu bar app.
// Left-click the menu bar icon, drag over an area of the screen, and the text
// in that area is recognized (Apple Vision) and copied to the clipboard.
// Right-click (or Control-click) the icon for options.

import Cocoa
import Vision
import ServiceManagement

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem!
    private var busy = false

    private let idleImage = NSImage(systemSymbolName: "text.viewfinder", accessibilityDescription: "Copy OCR")
    private let okImage = NSImage(systemSymbolName: "checkmark.circle.fill", accessibilityDescription: "Copied")
    private let emptyImage = NSImage(systemSymbolName: "xmark.circle", accessibilityDescription: "No text found")

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let button = statusItem.button {
            button.image = idleImage
            button.toolTip = "Click to copy text from a screen area\nRight-click for options"
            button.target = self
            button.action = #selector(statusItemClicked(_:))
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        }

        // Ask for Screen Recording permission up front so the first capture isn't blank.
        if !CGPreflightScreenCaptureAccess() {
            CGRequestScreenCaptureAccess()
        }
    }

    // MARK: - Status item

    @objc private func statusItemClicked(_ sender: NSStatusBarButton) {
        guard let event = NSApp.currentEvent else { return }
        if event.type == .rightMouseUp || event.modifierFlags.contains(.control) {
            showMenu()
        } else {
            capture()
        }
    }

    private func showMenu() {
        let menu = NSMenu()

        let captureItem = NSMenuItem(title: "Capture Text", action: #selector(capture), keyEquivalent: "")
        captureItem.target = self
        menu.addItem(captureItem)
        menu.addItem(.separator())

        let loginItem = NSMenuItem(title: "Launch at Login", action: #selector(toggleLaunchAtLogin), keyEquivalent: "")
        loginItem.target = self
        loginItem.state = SMAppService.mainApp.status == .enabled ? .on : .off
        menu.addItem(loginItem)
        menu.addItem(.separator())

        menu.addItem(NSMenuItem(title: "Quit Copy OCR", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))

        // Attach the menu just long enough to pop it up, so left-clicks keep triggering a capture.
        statusItem.menu = menu
        statusItem.button?.performClick(nil)
        statusItem.menu = nil
    }

    @objc private func toggleLaunchAtLogin() {
        do {
            if SMAppService.mainApp.status == .enabled {
                try SMAppService.mainApp.unregister()
            } else {
                try SMAppService.mainApp.register()
            }
        } catch {
            let alert = NSAlert()
            alert.messageText = "Couldn't change Launch at Login"
            alert.informativeText = error.localizedDescription
            NSApp.activate(ignoringOtherApps: true)
            alert.runModal()
        }
    }

    private func flash(_ image: NSImage?) {
        statusItem.button?.image = image
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) { [weak self] in
            guard let self else { return }
            self.statusItem.button?.image = self.idleImage
        }
    }

    // MARK: - Capture + OCR

    @objc private func capture() {
        guard !busy else { return }
        busy = true

        let file = FileManager.default.temporaryDirectory
            .appendingPathComponent("copyocr-\(UUID().uuidString).png")

        DispatchQueue.global(qos: .userInitiated).async {
            defer { try? FileManager.default.removeItem(at: file) }

            // The system's own area picker: drag to select, Space for window mode, Esc to cancel.
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
            process.arguments = ["-i", "-x", file.path]
            do {
                try process.run()
                process.waitUntilExit()
            } catch {
                DispatchQueue.main.async { self.finish(text: nil) }
                return
            }

            // No file means the user pressed Esc.
            guard let source = CGImageSourceCreateWithURL(file as CFURL, nil),
                  let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
                DispatchQueue.main.async { self.busy = false }
                return
            }

            let text = Self.recognizeText(in: image)
            DispatchQueue.main.async { self.finish(text: text) }
        }
    }

    private static func recognizeText(in image: CGImage) -> String {
        let request = VNRecognizeTextRequest()
        request.recognitionLevel = .accurate
        request.usesLanguageCorrection = true
        request.automaticallyDetectsLanguage = true

        let handler = VNImageRequestHandler(cgImage: image, options: [:])
        do {
            try handler.perform([request])
        } catch {
            return ""
        }

        let lines = (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }
        return lines.joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private func finish(text: String?) {
        busy = false
        guard let text, !text.isEmpty else {
            flash(emptyImage)
            NSSound.beep()
            return
        }
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        flash(okImage)
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
