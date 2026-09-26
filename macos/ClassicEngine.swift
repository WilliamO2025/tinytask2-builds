import AppKit
import ApplicationServices

struct MacroAction: Codable {
    var type: String
    var delay: Double = 0
    var x: Double = 0, y: Double = 0
    var button: UInt32 = 0
    var key: UInt16 = 0
    var flags: UInt64 = 0
    var down: Bool = false
    var delta: Int32 = 0, deltaX: Int32 = 0
    var pixelScroll: Bool = false
    var clickCount: Int64? = nil
}
struct MacroDocument: Codable {
    var version = 1
    var platform = "macos"
    var name = "Untitled macro"
    var actions: [MacroAction] = []
    func validate() throws {
        guard version == 1, platform == "macos" else { throw MacroError.message("Use this macro on its original platform. This app supports version 1 Mac macros.") }
        guard actions.count <= 100_000 else { throw MacroError.message("The macro has too many actions.") }
        for a in actions {
            if let count = a.clickCount, !(0...1000).contains(count) { throw MacroError.message("Invalid click count.") }
            guard ["move", "mouseDown", "mouseUp", "keyDown", "keyUp", "flags", "scroll", "delay"].contains(a.type), a.delay.isFinite, a.delay >= 0, a.delay <= 86_400, a.x.isFinite, a.y.isFinite, a.key <= 127, a.button <= 31 else { throw MacroError.message("The macro contains an invalid action.") }
        }
    }
    func data() throws -> Data { let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted, .sortedKeys]; let data = try encoder.encode(self); guard data.count <= 64 * 1024 * 1024 else { throw MacroError.message("Macro file is too large.") }; return data }
    static func load(_ data: Data) throws -> MacroDocument {
        guard data.count <= 64 * 1024 * 1024 else { throw MacroError.message("Macro files must be smaller than 64 MB.") }
        let macro = try JSONDecoder().decode(MacroDocument.self, from: data); try macro.validate(); return macro
    }
}
enum MacroError: LocalizedError { case message(String); var errorDescription: String? { if case let .message(text) = self { return text }; return nil } }

// Quartz events use global screen coordinates. This is Classic system input, not isolation.
final class ClassicEngine {
    private(set) var state = "Ready"
    private(set) var recording = MacroDocument()
    var changed: (() -> Void)?
    var failed: ((String) -> Void)?
    var control: ((Int) -> Void)?
    var recordKey: UInt16 = 100 // F8
    var playKey: UInt16 = 101 // F9
    let stopKey: UInt16 = 109 // F10
    private var tap: CFMachPort?
    private var tapSource: CFRunLoopSource?
    private var timer: Timer?
    private var lastRecord = 0.0, recordStart = 0.0, lastMove = 0.0
    private var started = 0.0, pausedAt = 0.0, due = 0.0
    private var preparedDisplays: [CGRect] = []
    private var displayLayout: [CGRect] { NSScreen.screens.compactMap { screen in (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber).map { CGDisplayBounds(CGDirectDisplayID($0.uint32Value)) } } }
    private var preparedData: Data?
    private var preparedSpeed = 0.0
    private var packets: [[CGEvent]] = []
    private var offsets: [Double] = []
    private var loopDuration = 0.0
    private var index = 0, loop = 0, loops = 1
    private var continuous = false, speed = 1.0
    private var macro = MacroDocument()
    private var held: [String: MacroAction] = [:]
    private let marker: Int64 = 0x545432
    var busy: Bool { state != "Ready" }
    var monitorReady: Bool { tap != nil }
    private func setState(_ value: String) { state = value; changed?() }
    func enableMonitor() throws {
        if tap != nil { return }
        guard CGPreflightListenEventAccess(), AXIsProcessTrusted() else { throw MacroError.message("Grant Accessibility and Input Monitoring in System Settings, then click Enable permissions again. A restart may be required.") }
        let types: [CGEventType] = [.mouseMoved, .leftMouseDragged, .rightMouseDragged, .otherMouseDragged, .leftMouseDown, .leftMouseUp, .rightMouseDown, .rightMouseUp, .otherMouseDown, .otherMouseUp, .keyDown, .keyUp, .flagsChanged, .scrollWheel]
        let mask = types.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        guard let port = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap, eventsOfInterest: mask, callback: { _, type, event, context in
            guard let context else { return Unmanaged.passUnretained(event) }
            let engine = Unmanaged<ClassicEngine>.fromOpaque(context).takeUnretainedValue()
            return engine.receive(type, event) ? nil : Unmanaged.passUnretained(event)
        }, userInfo: Unmanaged.passUnretained(self).toOpaque()) else { throw MacroError.message("macOS could not create the input monitor. Check both permissions and restart the app.") }
        tap = port; tapSource = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, port, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), tapSource, .commonModes); CGEvent.tapEnable(tap: port, enable: true)
    }
    private func receive(_ type: CGEventType, _ event: CGEvent) -> Bool {
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            stop(); if let tap { CGEvent.tapEnable(tap: tap, enable: true) }; failed?("Input monitoring was interrupted. The task stopped; start a new recording rather than using an incomplete one."); return false
        }
        if event.getIntegerValueField(.eventSourceUserData) == marker { return false }
        let key = UInt16(clamping: event.getIntegerValueField(.keyboardEventKeycode))
        if type == .keyDown || type == .keyUp {
            if [recordKey, playKey, stopKey].contains(key) {
                if type == .keyDown && event.getIntegerValueField(.keyboardEventAutorepeat) == 0 { let command = key == recordKey ? 0 : key == playKey ? 1 : 2; DispatchQueue.main.async { [weak self] in self?.control?(command) } }
                return true
            }
        }
        guard state == "Recording" else { return false }
        let keyboard = type == .keyDown || type == .keyUp || type == .flagsChanged
        if keyboard && NSApp.isActive { return false }
        let p = event.location
        if !keyboard {
            let cocoa = NSPoint(x: p.x, y: (NSScreen.screens.first?.frame.maxY ?? 0) - p.y)
            if NSApp.windows.contains(where: { $0.isVisible && !$0.isMiniaturized && $0.frame.contains(cocoa) }) { return false }
        }
        var a = MacroAction(type: "move", x: p.x, y: p.y, flags: event.flags.rawValue)
        switch type {
        case .leftMouseDown, .rightMouseDown, .otherMouseDown: a.type = "mouseDown"
        case .leftMouseUp, .rightMouseUp, .otherMouseUp: a.type = "mouseUp"
        case .keyDown: a.type = "keyDown"; a.key = key
        case .keyUp: a.type = "keyUp"; a.key = key
        case .flagsChanged: a.type = "flags"; a.key = key; a.down = CGEventSource.keyState(.combinedSessionState, key: key)
        case .scrollWheel:
            a.type = "scroll"; a.pixelScroll = event.getIntegerValueField(.scrollWheelEventIsContinuous) != 0
            a.delta = Int32(clamping: event.getIntegerValueField(a.pixelScroll ? .scrollWheelEventPointDeltaAxis1 : .scrollWheelEventDeltaAxis1))
            a.deltaX = Int32(clamping: event.getIntegerValueField(a.pixelScroll ? .scrollWheelEventPointDeltaAxis2 : .scrollWheelEventDeltaAxis2))
        default: break
        }
        if !keyboard { a.button = UInt32(clamping: event.getIntegerValueField(.mouseEventButtonNumber)) }
        if a.type == "mouseDown" || a.type == "mouseUp" { a.clickCount = event.getIntegerValueField(.mouseEventClickState) }
        let now = ProcessInfo.processInfo.systemUptime - recordStart
        if a.type == "move" && now - lastMove < 0.008 { return false }
        if a.type == "move" { lastMove = now }
        if recording.actions.count >= 99_700 { stop(); failed?("Recording stopped at the action limit. Save it before continuing."); return false }
        a.delay = max(0, now - lastRecord); lastRecord = now; recording.actions.append(a); return false
    }
    func record() throws {
        guard !busy else { return }; try enableMonitor()
        let formatter = DateFormatter(); formatter.dateFormat = "yyyy-MM-dd HH-mm-ss"
        recording = MacroDocument(name: "Macro " + formatter.string(from: Date())); recordStart = ProcessInfo.processInfo.systemUptime; lastRecord = 0; lastMove = 0; setState("Recording")
    }
    func play(_ document: MacroDocument, speed: Double, loops: Int, continuous: Bool, synchronizedStart: Double? = nil) throws {
        guard synchronizedStart?.isFinite ?? true else { throw MacroError.message("Invalid synchronized start time.") }
        guard !busy else { return }; try enableMonitor(); try document.validate()
        guard !document.actions.isEmpty, speed.isFinite, (0.01...1000).contains(speed), (1...1_000_000).contains(loops) else { throw MacroError.message("Choose a nonempty macro, speed 0.01–1000x and loops 1–1,000,000.") }
        guard !document.actions.contains(where: { ["keyDown", "keyUp"].contains($0.type) && [recordKey, playKey, stopKey].contains($0.key) }) else { throw MacroError.message("This macro contains a control hotkey. Change the recording/playback hotkeys first.") }
        guard CGEventSource.flagsState(.combinedSessionState).intersection([.maskShift, .maskControl, .maskAlternate, .maskCommand]).isEmpty else { throw MacroError.message("Release modifier keys before playback.") }
        if preparedData != (try document.data()) || preparedSpeed != speed || preparedDisplays != displayLayout { try prepare(document, speed: speed) }
        macro = document; self.speed = speed; self.loops = loops; self.continuous = continuous; index = 0; loop = 0
        started = synchronizedStart ?? ProcessInfo.processInfo.systemUptime; due = offsets[0]; setState("Playing")
        scheduleNext()
    }
    func prepare(_ document: MacroDocument, speed: Double) throws {
        guard !busy else { throw MacroError.message("Stop the current task first.") }
        try document.validate()
        guard !document.actions.isEmpty, speed.isFinite, (0.01...1000).contains(speed) else { throw MacroError.message("Choose a nonempty recording and valid speed.") }
        guard !document.actions.contains(where: { ["keyDown", "keyUp"].contains($0.type) && [recordKey, playKey, stopKey].contains($0.key) }) else { throw MacroError.message("The recording contains a playback control key.") }
        var original = 0.0
        let nextOffsets = document.actions.map { original += $0.delay; return original / speed }
        held.removeAll(); defer { held.removeAll() }
        var nextPackets: [[CGEvent]] = []
        for action in document.actions { nextPackets.append(try events(action)); track(action) }
        offsets = nextOffsets; loopDuration = original / speed; packets = nextPackets
        preparedData = try document.data(); preparedSpeed = speed; preparedDisplays = displayLayout
    }
    private func scheduleNext() {
        timer?.invalidate()
        guard state == "Playing" else { return }
        let wait = max(0.001, min(0.05, started + due - ProcessInfo.processInfo.systemUptime))
        timer = Timer(timeInterval: wait, repeats: false) { [weak self] _ in self?.tick() }
        timer!.tolerance = 0.0001; RunLoop.main.add(timer!, forMode: .common)
    }
    private func tick() {
        guard state == "Playing" else { return }
        do {
            guard preparedDisplays == displayLayout else { throw MacroError.message("Display layout changed. Playback stopped; prepare it again.") }
            var burst = 0
            while ProcessInfo.processInfo.systemUptime - started >= due && burst < 64 {
                let a = macro.actions[index]
                if ProcessInfo.processInfo.systemUptime - started - due > 0.5 { throw MacroError.message("Playback stopped because it fell too far behind the original timeline. Close busy applications and try again.") }
                for event in packets[index] { event.post(tap: .cghidEventTap) }; track(a); index += 1; burst += 1
                if index == macro.actions.count {
                    release(clear: true); loop += 1
                    if !continuous && loop >= loops { stop(); return }; index = 0
                }
                due = Double(loop) * loopDuration + offsets[index]
            }
            scheduleNext()
        } catch { stop(); failed?(error.localizedDescription) }
    }
    private func id(_ a: MacroAction) -> String { (a.type.hasPrefix("key") || a.type == "flags") ? "key:\(a.key)" : "mouse:\(a.button)" }
    private func track(_ a: MacroAction) {
        if a.type == "keyDown" || a.type == "mouseDown" || (a.type == "flags" && a.down && a.key != 57) { held[id(a)] = a }
        else if a.type == "keyUp" || a.type == "mouseUp" || a.type == "flags" { held.removeValue(forKey: id(a)) }
    }
    static func resumeOrder(_ inputs: [MacroAction]) -> [MacroAction] {
        func rank(_ action: MacroAction) -> Int {
            let modifierKeys: [UInt16] = [54,55,56,58,59,60,61,62,63]
            return action.type == "flags" || (action.type == "keyDown" && modifierKeys.contains(action.key)) ? 0 : action.type == "keyDown" ? 1 : 2
        }
        return inputs.sorted { rank($0) != rank($1) ? rank($0) < rank($1) : $0.key != $1.key ? $0.key < $1.key : $0.button < $1.button }
    }
    func pauseResume() {
        if state == "Playing" { pausedAt = ProcessInfo.processInfo.systemUptime; timer?.fireDate = .distantFuture; setState("Paused"); release(clear: false) }
        else if state == "Paused" {
            do { for a in Self.resumeOrder(Array(held.values)) { try send(a) }; started += ProcessInfo.processInfo.systemUptime - pausedAt; setState("Playing"); scheduleNext() }
            catch { stop(); failed?(error.localizedDescription) }
        }
    }
    func stop() {
        timer?.invalidate(); timer = nil
        if state == "Recording" {
            recording.actions.append(MacroAction(type: "delay", delay: max(0, ProcessInfo.processInfo.systemUptime - recordStart - lastRecord)))
            held.removeAll(); for a in recording.actions { track(a) }
            for a in held.values { recording.actions.append(up(a)) }; held.removeAll()
        } else { release(clear: true) }
        setState("Ready")
    }
    private func up(_ a: MacroAction) -> MacroAction { var result = a; result.type = a.type == "mouseDown" ? "mouseUp" : a.type == "flags" ? "flags" : "keyUp"; result.down = false; result.flags = 0; result.delay = 0; return result }
    private func release(clear: Bool) { for a in held.values { try? send(up(a), release: true) }; if clear { held.removeAll() } }
    private func send(_ a: MacroAction, release: Bool = false) throws { for event in try events(a, release: release) { event.post(tap: .cghidEventTap) } }
    private func events(_ a: MacroAction, release: Bool = false) throws -> [CGEvent] {
        if a.type == "delay" { return [] }
        var result: [CGEvent] = []
        var event: CGEvent?
        if a.type.hasPrefix("key") || a.type == "flags" {
            event = CGEvent(keyboardEventSource: nil, virtualKey: a.key, keyDown: a.type == "keyDown" || (a.type == "flags" && a.down))
            if a.type == "flags" { event?.type = .flagsChanged }
        } else {
            let point = release ? (CGEvent(source: nil)?.location ?? CGPoint(x: a.x, y: a.y)) : CGPoint(x: a.x, y: a.y)
            let onScreen = NSScreen.screens.contains { screen in guard let number = screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber else { return false }; return CGDisplayBounds(CGDirectDisplayID(number.uint32Value)).contains(point) }
            guard onScreen || release else { throw MacroError.message("A macro position is outside the current display layout.") }
            if a.type == "scroll" {
                let move = CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: point, mouseButton: .left); move?.setIntegerValueField(.eventSourceUserData, value: marker); if let move { result.append(move) }
                event = CGEvent(scrollWheelEvent2Source: nil, units: a.pixelScroll ? .pixel : .line, wheelCount: 2, wheel1: a.delta, wheel2: a.deltaX, wheel3: 0)
            } else {
                let button = CGMouseButton(rawValue: a.button) ?? .left
                let type: CGEventType
                if a.type == "move" { type = held["mouse:\(a.button)"] == nil ? .mouseMoved : a.button == 0 ? .leftMouseDragged : a.button == 1 ? .rightMouseDragged : .otherMouseDragged }
                else if a.button == 0 { type = a.type == "mouseDown" ? .leftMouseDown : .leftMouseUp }
                else if a.button == 1 { type = a.type == "mouseDown" ? .rightMouseDown : .rightMouseUp }
                else { type = a.type == "mouseDown" ? .otherMouseDown : .otherMouseUp }
                event = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point, mouseButton: button)
            }
        }
        guard let event else { throw MacroError.message("macOS could not create a playback event.") }
        if a.type == "mouseDown" || a.type == "mouseUp" { event.setIntegerValueField(.mouseEventClickState, value: a.clickCount ?? 1) }
        event.flags = CGEventFlags(rawValue: a.flags); event.setIntegerValueField(.eventSourceUserData, value: marker); result.append(event); return result
    }
    func shutdown() { stop(); if let source = tapSource { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }; if let tap { CFMachPortInvalidate(tap) }; tap = nil; tapSource = nil }
}
