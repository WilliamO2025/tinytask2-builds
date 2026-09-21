// Native macOS experiment. Requires a Mac for compilation and permission/behavior tests.
import AppKit
import ApplicationServices
import IOKit.hid

final class InputLab: NSObject, NSApplicationDelegate, NSWindowDelegate {
    let window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 780, height: 560), styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
    let targets = NSPopUpButton()
    let devices = NSPopUpButton()
    let x = NSTextField(string: "100"), y = NSTextField(string: "100")
    let status = NSTextField(wrappingLabelWithString: "Observation off. Input isolation and independent OS focus are unavailable.")
    let cursor = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 32, height: 36), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
    var apps: [NSRunningApplication] = []
    var hid: IOHIDManager?
    var sourceCount = 0
    var timer: Timer?
    var localMonitor: Any?, globalMonitor: Any?
    var task: DispatchWorkItem?
    var statusItem: NSStatusItem!
    var records: [[String: Any]] = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        if CommandLine.arguments.contains("--dark") { NSApp.appearance = NSAppearance(named: .darkAqua) }
        else if CommandLine.arguments.contains("--light") { NSApp.appearance = NSAppearance(named: .aqua) }
        window.title = "TinyTask 2.0 — Input Lab 0.1 (experimental)"
        window.delegate = self
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 12
        stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 24), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -24), stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 24)])
        let title = NSTextField(labelWithString: "TinyTask 2.0 / Input Lab"); title.font = .boldSystemFont(ofSize: 26)
        stack.addArrangedSubview(title)
        stack.addArrangedSubview(NSTextField(wrappingLabelWithString: "User-mode compatibility probes. Coordinates are global Quartz screen points. No physical input is blocked or rerouted."))
        stack.addArrangedSubview(targets); stack.addArrangedSubview(devices)
        stack.addArrangedSubview(NSStackView(views: [button("Refresh apps/devices", #selector(refresh)), button("Request permissions", #selector(permissions)), button("Observe devices", #selector(observe))]))
        x.widthAnchor.constraint(equalToConstant: 90).isActive = true; y.widthAnchor.constraint(equalToConstant: 90).isActive = true
        stack.addArrangedSubview(NSStackView(views: [NSTextField(labelWithString: "Screen X"), x, NSTextField(labelWithString: "Y"), y, button("Show visual cursor", #selector(showCursor))]))
        stack.addArrangedSubview(NSStackView(views: [button("Targeted click in 3s", #selector(click)), button("AX Press in 3s", #selector(press)), button("Stop / release", #selector(stop))]))
        stack.addArrangedSubview(button("Export JSON report", #selector(exportReport)))
        stack.addArrangedSubview(status)
        stack.addArrangedSubview(NSTextField(wrappingLabelWithString: "Emergency: Control + Option + F12. Global shortcut requires Input Monitoring permission. Closing exits completely; minimizing leaves the menu-bar controls available."))
        cursor.isOpaque = false; cursor.backgroundColor = .clear; cursor.hasShadow = false; cursor.ignoresMouseEvents = true; cursor.level = .floating
        let image = NSImageView(frame: NSRect(x: 0, y: 0, width: 32, height: 36)); image.image = NSCursor.arrow.image; cursor.contentView = image
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength); statusItem.button?.title = "TT²"
        let menu = NSMenu(); menu.addItem(withTitle: "Open Input Lab", action: #selector(openWindow), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Stop / release", action: #selector(stop), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Quit", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"); statusItem.menu = menu
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in if self?.emergency(event) == true { return nil }; return event }
        globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { [weak self] event in _ = self?.emergency(event) }
        refresh(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    func button(_ title: String, _ action: Selector) -> NSButton { NSButton(title: title, target: self, action: action) }
    func emergency(_ event: NSEvent) -> Bool { if event.keyCode == 111 && event.modifierFlags.contains([.control, .option]) { stop(); return true }; return false }
    @objc func openWindow() { window.deminiaturize(nil); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }
    @objc func permissions() {
        _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary)
        _ = CGRequestListenEventAccess()
        status.stringValue = "Grant Accessibility and Input Monitoring in System Settings, then restart Input Lab. No permission is bypassed."
    }
    @objc func refresh() {
        apps = NSWorkspace.shared.runningApplications.filter { $0.activationPolicy == .regular && $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }
        targets.removeAllItems(); targets.addItems(withTitles: apps.map { "\($0.localizedName ?? "App") • PID \($0.processIdentifier)" })
        let manager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone))
        IOHIDManagerSetDeviceMatching(manager, nil)
        devices.removeAllItems()
        if let found = IOHIDManagerCopyDevices(manager) as? Set<IOHIDDevice> {
            for device in found { let product = IOHIDDeviceGetProperty(device, kIOHIDProductKey as CFString).map { String(describing: $0) } ?? "HID"; devices.addItem(withTitle: product) }
        }
        if devices.numberOfItems == 0 { devices.addItem(withTitle: "No HID devices enumerated; check permissions") }
        IOHIDManagerClose(manager, IOOptionBits(kIOHIDOptionsTypeNone))
    }
    @objc func observe() {
        stop()
        guard CGPreflightListenEventAccess() else { status.stringValue = "Input Monitoring permission is required."; return }
        let manager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone)); hid = manager
        IOHIDManagerSetDeviceMatchingMultiple(manager, [[kIOHIDDeviceUsagePageKey: 1, kIOHIDDeviceUsageKey: 2], [kIOHIDDeviceUsagePageKey: 1, kIOHIDDeviceUsageKey: 6]] as CFArray)
        IOHIDManagerRegisterInputValueCallback(manager, { context, _, _, value in
            guard let context else { return }; let owner = Unmanaged<InputLab>.fromOpaque(context).takeUnretainedValue()
            owner.sourceCount += 1
            let device = IOHIDElementGetDevice(IOHIDValueGetElement(value))
            let name = IOHIDDeviceGetProperty(device, kIOHIDProductKey as CFString).map { String(describing: $0) } ?? "HID"
            owner.lastSource = name
        }, Unmanaged.passUnretained(self).toOpaque())
        IOHIDManagerScheduleWithRunLoop(manager, CFRunLoopGetMain(), CFRunLoopMode.commonModes.rawValue)
        let result = IOHIDManagerOpen(manager, IOOptionBits(kIOHIDOptionsTypeNone))
        guard result == kIOReturnSuccess else { stop(); status.stringValue = "HID observation denied: \(result)"; return }
        timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in guard let self else { return }; self.status.stringValue = "Observed \(self.sourceCount) events; source: \(self.lastSource). Physical input is still shared." }
    }
    var lastSource = "none"
    func location() -> CGPoint? { guard let px = Double(x.stringValue), let py = Double(y.stringValue), px.isFinite, py.isFinite else { status.stringValue = "Enter finite screen coordinates."; return nil }; return CGPoint(x: px, y: py) }
    @objc func showCursor() { guard let point = location() else { return }; let height = NSScreen.screens.first?.frame.height ?? 0; cursor.setFrameOrigin(NSPoint(x: point.x, y: height - point.y - 36)); cursor.orderFrontRegardless() }
    @objc func click() { schedule(useAX: false) }
    @objc func press() { schedule(useAX: true) }
    func schedule(useAX: Bool) {
        guard AXIsProcessTrusted(), CGPreflightListenEventAccess() else { status.stringValue = "Grant permissions first so the emergency shortcut can be monitored."; return }
        guard targets.indexOfSelectedItem >= 0, targets.indexOfSelectedItem < apps.count, let point = location() else { return }
        let app = apps[targets.indexOfSelectedItem]; task?.cancel()
        status.stringValue = "Probe in 3 seconds. Switch to your work app."
        let item = DispatchWorkItem { [weak self] in
            guard let self, !app.isTerminated else { return }
            let before = NSWorkspace.shared.frontmostApplication?.processIdentifier ?? 0
            var result = "Posted; target effect unverified"
            if useAX {
                let root = AXUIElementCreateApplication(app.processIdentifier); _ = AXUIElementSetMessagingTimeout(root, 2); var element: AXUIElement?
                let found = AXUIElementCopyElementAtPosition(root, Float(point.x), Float(point.y), &element)
                if found == .success, let element { _ = AXUIElementSetMessagingTimeout(element, 2); result = "AXPress result: \(AXUIElementPerformAction(element, kAXPressAction as CFString).rawValue); verify effect" } else { result = "Target accessibility element unavailable" }
            } else if let down = CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: point, mouseButton: .left), let up = CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: point, mouseButton: .left) {
                down.postToPid(app.processIdentifier); up.postToPid(app.processIdentifier)
            } else { result = "Event allocation failed" }
            self.records.append(["time": ISO8601DateFormatter().string(from: Date()), "pid": app.processIdentifier, "method": useAX ? "AXPress" : "CGEventPostToPid", "result": result, "foregroundBefore": before, "foregroundAfter": NSWorkspace.shared.frontmostApplication?.processIdentifier ?? 0])
            self.status.stringValue = result
        }
        task = item; DispatchQueue.main.asyncAfter(deadline: .now() + 3, execute: item)
    }
    @objc func stop() { task?.cancel(); task = nil; timer?.invalidate(); timer = nil; cursor.orderOut(nil); if let hid { IOHIDManagerUnscheduleFromRunLoop(hid, CFRunLoopGetMain(), CFRunLoopMode.commonModes.rawValue); IOHIDManagerClose(hid, IOOptionBits(kIOHIDOptionsTypeNone)) }; hid = nil; status.stringValue = "Stopped. No device was seized or blocked." }
    @objc func exportReport() { let save = NSSavePanel(); save.nameFieldStringValue = "TinyTask-mac-input-report.json"; if save.runModal() == .OK, let url = save.url { do { let data = try JSONSerialization.data(withJSONObject: ["version": "0.1.0", "platform": "macOS", "physicalSuppression": false, "observations": records], options: [.prettyPrinted, .sortedKeys]); try data.write(to: url, options: .atomic) } catch { status.stringValue = error.localizedDescription } } }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationWillTerminate(_ notification: Notification) { stop(); if let localMonitor { NSEvent.removeMonitor(localMonitor) }; if let globalMonitor { NSEvent.removeMonitor(globalMonitor) } }
}
let app = NSApplication.shared
let delegate = InputLab()
app.setActivationPolicy(.regular)
app.delegate = delegate
app.run()
