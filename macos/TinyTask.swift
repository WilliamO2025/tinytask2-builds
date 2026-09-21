import AppKit
import ApplicationServices
import UniformTypeIdentifiers

final class TinyTaskApp: NSObject, NSApplicationDelegate, NSWindowDelegate {
    let engine = ClassicEngine()
    let window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 720, height: 365), styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
    let mode = NSPopUpButton()
    let name = NSTextField(labelWithString: "No macro open")
    let detail = NSTextField(labelWithString: "Record a task or open a saved macro.")
    let status = NSTextField(wrappingLabelWithString: "Ready")
    let speed = NSComboBox()
    let loops = NSTextField(string: "1")
    let continuous = NSButton(checkboxWithTitle: "Continuous", target: nil, action: nil)
    let advanced = NSStackView()
    var recordButton: NSButton!, playButton: NSButton!, pauseButton: NSButton!, openButton: NSButton!, saveButton: NSButton!, preferencesButton: NSButton!
    var statusItem: NSStatusItem!
    var macro: MacroDocument?
    var wasRecording = false
    let defaults = UserDefaults.standard
    var library: URL { FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("TinyTask2/Macros", isDirectory: true) }
    func applicationDidFinishLaunching(_ notification: Notification) {
        window.title = "TinyTask 2.0"; window.delegate = self
        engine.recordKey = UInt16(defaults.object(forKey: "recordKey") as? Int ?? 100); engine.playKey = UInt16(defaults.object(forKey: "playKey") as? Int ?? 101)
        engine.changed = { [weak self] in self?.update() }; engine.failed = { [weak self] text in self?.status.stringValue = text }
        engine.control = { [weak self] command in if command == 0 { self?.record() } else if command == 1 { self?.playPause() } else { self?.stop() } }
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 16; stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 24), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -24), stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 22)])
        let title = NSTextField(labelWithString: "TinyTask 2.0"); title.font = .systemFont(ofSize: 27, weight: .semibold)
        mode.addItems(withTitles: ["Classic", "Advanced"]); mode.target = self; mode.action = #selector(modeChanged)
        preferencesButton = button("Preferences", #selector(preferences))
        stack.addArrangedSubview(row([title, mode, preferencesButton]))
        openButton = button("Open", #selector(open)); saveButton = button("Save", #selector(save)); recordButton = button("● Record", #selector(record)); playButton = button("▶ Play", #selector(playPause)); pauseButton = button("Pause", #selector(pause)); let stopButton = button("■ Stop", #selector(stop))
        for control in [openButton!, saveButton!, recordButton!, playButton!, pauseButton!, stopButton] { control.widthAnchor.constraint(equalToConstant: 94).isActive = true }
        stack.addArrangedSubview(row([openButton, saveButton, recordButton, playButton, pauseButton, stopButton]))
        name.font = .systemFont(ofSize: 20, weight: .semibold); stack.addArrangedSubview(name)
        detail.textColor = .secondaryLabelColor; stack.addArrangedSubview(detail)
        speed.addItems(withObjectValues: ["0.5", "1", "2", "10", "100"]); speed.stringValue = defaults.string(forKey: "speed") ?? "1"; speed.widthAnchor.constraint(equalToConstant: 85).isActive = true
        loops.stringValue = defaults.string(forKey: "loops") ?? "1"; loops.widthAnchor.constraint(equalToConstant: 65).isActive = true; continuous.state = defaults.bool(forKey: "continuous") ? .on : .off
        stack.addArrangedSubview(row([NSTextField(labelWithString: "Speed"), speed, NSTextField(labelWithString: "×    Loops"), loops, continuous]))
        let note = NSTextField(wrappingLabelWithString: "Classic playback controls your mouse and keyboard. Starts after 3 seconds. F10 stops."); note.textColor = .secondaryLabelColor; stack.addArrangedSubview(note)
        advanced.orientation = .vertical; advanced.alignment = .leading; advanced.spacing = 12
        advanced.addArrangedSubview(NSTextField(wrappingLabelWithString: "Advanced Mode is experimental. Background input, second-mouse isolation and virtual focus are unavailable on macOS. Use Classic to record and play."))
        advanced.addArrangedSubview(button("Open Input Lab diagnostics", #selector(diagnostics))); advanced.isHidden = true; stack.addArrangedSubview(advanced)
        stack.addArrangedSubview(status)
        makeMenus(); applyAppearance(); window.level = defaults.bool(forKey: "alwaysOnTop") ? .floating : .normal
        let recovery = library.deletingLastPathComponent().appendingPathComponent("last-recording.json")
        if let data = try? Data(contentsOf: recovery), let saved = try? MacroDocument.load(data) { macro = saved; name.stringValue = saved.name; detail.stringValue = "\(saved.actions.count) actions" }
        update(); window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        do { try engine.enableMonitor() } catch { status.stringValue = "First use: open Preferences → Enable permissions to use recording, playback and hotkeys." }
        if let argument = CommandLine.arguments.firstIndex(of: "--ui-smoke"), CommandLine.arguments.count > argument + 1 {
            let output = CommandLine.arguments[argument + 1]
            NSApp.appearance = NSAppearance(named: CommandLine.arguments.contains("dark") ? .darkAqua : .aqua)
            macro = nil; update(); playButton.performClick(nil)
            guard playButton.isEnabled && status.stringValue == "No recording available. Record or open a macro first." else { exit(1) }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                guard let view = self.window.contentView?.superview, let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { exit(1) }
                view.effectiveAppearance.performAsCurrentDrawingAppearance {
                    view.layoutSubtreeIfNeeded(); view.displayIfNeeded(); view.cacheDisplay(in: view.bounds, to: bitmap)
                }
                do { guard let data = bitmap.representation(using: .png, properties: [:]) else { exit(1) }; try data.write(to: URL(fileURLWithPath: output)); NSApp.terminate(nil) } catch { fputs(error.localizedDescription + "\n", stderr); exit(1) }
            }
        }
    }
    func button(_ title: String, _ action: Selector) -> NSButton { let b = NSButton(title: title, target: self, action: action); b.bezelStyle = .rounded; b.controlSize = .large; return b }
    func row(_ views: [NSView]) -> NSStackView { let row = NSStackView(views: views); row.spacing = 10; row.alignment = .centerY; return row }
    func makeMenus() {
        let main = NSMenu(); NSApp.mainMenu = main
        let appMenu = NSMenu(); let appItem = NSMenuItem(); main.addItem(appItem); appItem.submenu = appMenu
        appMenu.addItem(withTitle: "About TinyTask 2.0", action: #selector(NSApplication.orderFrontStandardAboutPanel(_:)), keyEquivalent: "")
        appMenu.addItem(withTitle: "Preferences…", action: #selector(preferences), keyEquivalent: ",").target = self; appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Quit TinyTask 2.0", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let file = NSMenu(title: "File"); let fileItem = NSMenuItem(title: "File", action: nil, keyEquivalent: ""); main.addItem(fileItem); fileItem.submenu = file
        file.addItem(withTitle: "Open…", action: #selector(open), keyEquivalent: "o").target = self; file.addItem(withTitle: "Save As…", action: #selector(save), keyEquivalent: "s").target = self
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength); statusItem.button?.title = "TT²"
        let menu = NSMenu(); menu.addItem(withTitle: "Open TinyTask 2.0", action: #selector(show), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Record / finish", action: #selector(record), keyEquivalent: "").target = self; menu.addItem(withTitle: "Play / pause", action: #selector(playPause), keyEquivalent: "").target = self; menu.addItem(withTitle: "Stop", action: #selector(stop), keyEquivalent: "").target = self; menu.addItem(.separator()); menu.addItem(withTitle: "Quit", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"); statusItem.menu = menu
    }
    func update() {
        if wasRecording && engine.state != "Recording" {
            macro = engine.recording; name.stringValue = engine.recording.name; detail.stringValue = "\(engine.recording.actions.count) actions"
            do {
                try FileManager.default.createDirectory(at: library, withIntermediateDirectories: true)
                let data = try engine.recording.data(); try data.write(to: library.appendingPathComponent(engine.recording.name + "-" + String(UUID().uuidString.prefix(8)) + ".json"), options: .atomic)
                try data.write(to: library.deletingLastPathComponent().appendingPathComponent("last-recording.json"), options: .atomic)
            } catch { DispatchQueue.main.async { [weak self] in self?.status.stringValue = "Recording kept in memory; auto-save failed: " + error.localizedDescription } }
        }
        wasRecording = engine.state == "Recording"; status.stringValue = engine.state
        recordButton.title = wasRecording ? "Finish" : "● Record"; recordButton.isEnabled = mode.indexOfSelectedItem == 0 && ["Ready", "Recording"].contains(engine.state)
        playButton.isEnabled = mode.indexOfSelectedItem == 0 && ["Ready", "Paused"].contains(engine.state)
        pauseButton.title = engine.state == "Paused" ? "Resume" : "Pause"; pauseButton.isEnabled = ["Playing", "Paused"].contains(engine.state)
        openButton.isEnabled = !engine.busy; saveButton.isEnabled = !engine.busy && macro != nil; preferencesButton.isEnabled = !engine.busy; mode.isEnabled = !engine.busy
    }
    @objc func record() {
        if engine.state == "Recording" { engine.stop(); return }
        guard mode.indexOfSelectedItem == 0 else { status.stringValue = "Switch to Classic to record."; return }
        do { try engine.record() } catch { status.stringValue = error.localizedDescription }
    }
    @objc func playPause() {
        if ["Playing", "Paused"].contains(engine.state) { engine.pauseResume(); return }
        guard let macro, !macro.actions.isEmpty else { status.stringValue = "No recording available. Record or open a macro first."; return }
        guard mode.indexOfSelectedItem == 0 else { status.stringValue = "Switch to Classic to play."; return }
        do { guard let rate = Double(speed.stringValue), let count = Int(loops.stringValue) else { throw MacroError.message("Enter a numeric speed and whole-number loop count.") }; saveSettings(); try engine.play(macro, speed: rate, loops: count, continuous: continuous.state == .on) } catch { status.stringValue = error.localizedDescription }
    }
    @objc func pause() { engine.pauseResume() }
    @objc func stop() { engine.stop() }
    @objc func modeChanged() { advanced.isHidden = mode.indexOfSelectedItem == 0; window.setContentSize(NSSize(width: 720, height: advanced.isHidden ? 365 : 465)); update() }
    @objc func show() { window.deminiaturize(nil); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    @objc func open() {
        guard !engine.busy else { return }; let panel = NSOpenPanel(); panel.allowedContentTypes = [.json]; panel.directoryURL = library; panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.beginSheetModal(for: window) { [weak self] result in
            guard result == .OK, let url = panel.url, let self else { return }
            do { let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0; guard size <= 64 * 1024 * 1024 else { throw MacroError.message("Macro file is too large.") }; let opened = try MacroDocument.load(Data(contentsOf: url)); self.macro = opened; self.name.stringValue = url.deletingPathExtension().lastPathComponent; self.detail.stringValue = "\(opened.actions.count) actions"; self.update() } catch { self.status.stringValue = error.localizedDescription }
        }
    }
    @objc func save() {
        guard !engine.busy, var macro else { return }; let panel = NSSavePanel(); panel.allowedContentTypes = [.json]; panel.directoryURL = library; panel.nameFieldStringValue = name.stringValue + ".json"
        panel.beginSheetModal(for: window) { [weak self] result in
            guard result == .OK, let url = panel.url else { return }; do { macro.name = url.deletingPathExtension().lastPathComponent; try macro.data().write(to: url, options: .atomic); self?.macro = macro; self?.name.stringValue = macro.name } catch { self?.status.stringValue = error.localizedDescription }
        }
    }
    @objc func permissions() {
        _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary); _ = CGRequestListenEventAccess(); _ = CGRequestPostEventAccess()
        do { try engine.enableMonitor(); status.stringValue = "Permissions ready. F8 records, F9 plays/pauses, F10 stops (or your chosen keys)." } catch { status.stringValue = error.localizedDescription }
    }
    @objc func preferences() {
        guard !engine.busy else { return }
        let alert = NSAlert(); alert.messageText = "Preferences"; alert.informativeText = "F10 always stops. Some keyboards require Fn with function keys.\nAccessibility and Input Monitoring are required; secure input fields may prevent recording."
        let top = NSButton(checkboxWithTitle: "Always on top", target: nil, action: nil); top.state = window.level == .floating ? .on : .off
        let keys: [(String, UInt16)] = [("F1",122),("F2",120),("F3",99),("F4",118),("F5",96),("F6",97),("F7",98),("F8",100),("F9",101),("F11",103),("F12",111)]
        let record = NSPopUpButton(), play = NSPopUpButton(), theme = NSPopUpButton()
        record.addItems(withTitles: keys.map { $0.0 }); play.addItems(withTitles: keys.map { $0.0 }); record.selectItem(at: keys.firstIndex { $0.1 == engine.recordKey } ?? 7); play.selectItem(at: keys.firstIndex { $0.1 == engine.playKey } ?? 8)
        theme.addItems(withTitles: ["System", "Light", "Dark"]); theme.selectItem(at: defaults.integer(forKey: "theme"))
        let controls = NSStackView(views: [top, row([NSTextField(labelWithString: "Recording hotkey"), record]), row([NSTextField(labelWithString: "Playback hotkey"), play]), row([NSTextField(labelWithString: "Appearance"), theme]), button("Enable permissions", #selector(permissions)), button("Open saved macros folder", #selector(savedMacros)), button("Advanced → Diagnostics", #selector(diagnostics))]); controls.orientation = .vertical; controls.alignment = .leading; controls.spacing = 12; controls.frame = NSRect(x: 0, y: 0, width: 390, height: 280); alert.accessoryView = controls; alert.addButton(withTitle: "Save"); alert.addButton(withTitle: "Cancel")
        if alert.runModal() == .alertFirstButtonReturn {
            guard record.indexOfSelectedItem != play.indexOfSelectedItem else { status.stringValue = "Choose different hotkeys for recording and playback."; return }
            engine.recordKey = keys[record.indexOfSelectedItem].1; engine.playKey = keys[play.indexOfSelectedItem].1; defaults.set(Int(engine.recordKey), forKey: "recordKey"); defaults.set(Int(engine.playKey), forKey: "playKey"); defaults.set(top.state == .on, forKey: "alwaysOnTop"); defaults.set(theme.indexOfSelectedItem, forKey: "theme"); window.level = top.state == .on ? .floating : .normal; saveSettings(); applyAppearance()
        }
    }
    @objc func savedMacros() { do { try FileManager.default.createDirectory(at: library, withIntermediateDirectories: true); NSWorkspace.shared.open(library) } catch { status.stringValue = error.localizedDescription } }
    @objc func diagnostics() {
        guard !engine.busy else { return }; let adjacent = Bundle.main.bundleURL.deletingLastPathComponent().appendingPathComponent("TinyTask 2.0 Input Lab.app"); let installed = URL(fileURLWithPath: "/Applications/TinyTask 2.0 Input Lab.app"); let url = FileManager.default.fileExists(atPath: adjacent.path) ? adjacent : installed
        guard FileManager.default.fileExists(atPath: url.path) else { status.stringValue = "The separate Input Lab app is not installed. Advanced input is experimental."; return }
        let configuration = NSWorkspace.OpenConfiguration(); configuration.arguments = [NSApp.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? "--dark" : "--light"]
        NSWorkspace.shared.openApplication(at: url, configuration: configuration) { [weak self] _, error in if let error { DispatchQueue.main.async { self?.status.stringValue = error.localizedDescription } } }
    }
    func applyAppearance() { NSApp.appearance = defaults.integer(forKey: "theme") == 1 ? NSAppearance(named: .aqua) : defaults.integer(forKey: "theme") == 2 ? NSAppearance(named: .darkAqua) : nil }
    func saveSettings() { defaults.set(speed.stringValue, forKey: "speed"); defaults.set(loops.stringValue, forKey: "loops"); defaults.set(continuous.state == .on, forKey: "continuous") }
    func windowShouldClose(_ sender: NSWindow) -> Bool { NSApp.terminate(nil); return false }
    func applicationWillTerminate(_ notification: Notification) { saveSettings(); engine.shutdown(); if let statusItem { NSStatusBar.system.removeStatusItem(statusItem) } }
}

@main struct TinyTaskMain {
    static func main() {
        if CommandLine.arguments.contains("--self-test") {
            do {
                let original = MacroDocument(name: "Round trip", actions: [MacroAction(type: "mouseDown", delay: 0.25, x: 123, y: 456), MacroAction(type: "keyUp", key: 0)])
                let decoded = try MacroDocument.load(original.data()); guard decoded.actions.count == 2 && decoded.actions[0].delay == 0.25 else { throw MacroError.message("Round-trip failed") }
                let resume = ClassicEngine.resumeOrder([MacroAction(type: "keyDown", key: 0), MacroAction(type: "flags", key: 56, down: true)])
                guard resume.first?.key == 56 else { throw MacroError.message("Modifier resume order failed") }
                var invalid = original; invalid.actions[0].delay = -1; var rejected = false; do { try invalid.validate() } catch { rejected = true }; guard rejected else { throw MacroError.message("Invalid delay accepted") }
                print("{\"passed\":true,\"checks\":[\"macro round trip\",\"invalid delay rejected\",\"modifier resume ordering\"],\"scope\":\"No physical input or permission test\"}"); return
            } catch { fputs(error.localizedDescription + "\n", stderr); exit(1) }
        }
        let app = NSApplication.shared; app.setActivationPolicy(.regular); let delegate = TinyTaskApp(); app.delegate = delegate; app.run(); withExtendedLifetime(delegate) {}
    }
}
