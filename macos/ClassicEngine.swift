import AppKit
import ApplicationServices

struct MacroAction: Codable, Equatable {
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
struct MacroDocument: Codable, Equatable {
    var version = 1
    var platform = "macos"
    var name = "Untitled macro"
    var actions: [MacroAction] = []
    func validate() throws {
        guard version == 1, platform == "macos" else { throw MacroError.message("Use this macro on its original platform. This app supports version 1 Mac macros.") }
        for a in actions {
            if let count = a.clickCount, !(0...1000).contains(count) { throw MacroError.message("Invalid click count.") }
            guard ["move", "mouseDown", "mouseUp", "keyDown", "keyUp", "flags", "scroll", "delay"].contains(a.type), a.delay.isFinite, a.delay >= 0, a.delay <= 86_400, a.x.isFinite, a.y.isFinite, a.key <= 127, a.button <= 31 else { throw MacroError.message("The macro contains an invalid action.") }
        }
    }
    func data() throws -> Data { let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted, .sortedKeys]; return try encoder.encode(self) }
    static func load(_ data: Data) throws -> MacroDocument {
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
    private var lastRecord = 0.0, recordStart = 0.0
    private var started = 0.0, pausedAt = 0.0, due = 0.0
    private var preparedDisplays: [CGRect] = []
    private var displayLayout: [CGRect] { NSScreen.screens.compactMap { screen in (screen.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber).map { CGDisplayBounds(CGDirectDisplayID($0.uint32Value)) } } }
    private var preparedDocument: MacroDocument?
    private var preparationHeld: [String: MacroAction] = [:]
    private var nextPrepared = 0
    private(set) var capturedMoves = 0, capturedClicks = 0, capturedKeys = 0
    var preparedPacketCapacity: Int { packets.count }
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
        if let tap {
            if !CGEvent.tapIsEnabled(tap: tap) { CGEvent.tapEnable(tap: tap, enable: true) }
            if CGEvent.tapIsEnabled(tap: tap) { return }
            throw MacroError.message("The input monitor is disabled. Fully quit and reopen this copy after approving access.")
        }
        // The real filtering tap is authoritative; preflight flags can lag a permission change.
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
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            DispatchQueue.main.async { [weak self] in
                self?.stop(); self?.failed?("Input monitoring was interrupted. The task stopped; start a new recording rather than using an incomplete one.")
            }
            return false
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
        if !keyboard && ![CGEventType.mouseMoved, .leftMouseDragged, .rightMouseDragged, .otherMouseDragged].contains(type) {
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
        if !keyboard && type != .mouseMoved { a.button = UInt32(clamping: event.getIntegerValueField(.mouseEventButtonNumber)) }
        if a.type == "mouseDown" || a.type == "mouseUp" { a.clickCount = event.getIntegerValueField(.mouseEventClickState) }
        let now = ProcessInfo.processInfo.systemUptime - recordStart
        a.delay = max(0, now - lastRecord); lastRecord = now; recording.actions.append(a)
        if a.type == "move" { capturedMoves += 1 }; if a.type == "mouseDown" { capturedClicks += 1 }; if a.type == "keyDown" { capturedKeys += 1 }; return false
    }
    func record() throws {
        guard !busy else { return }; try enableMonitor()
        let formatter = DateFormatter(); formatter.dateFormat = "yyyy-MM-dd HH-mm-ss"
        recording = MacroDocument(name: "Macro " + formatter.string(from: Date())); recordStart = ProcessInfo.processInfo.systemUptime; lastRecord = 0; capturedMoves = 0; capturedClicks = 0; capturedKeys = 0; setState("Recording")
    }
    func play(_ document: MacroDocument, speed: Double, loops: Int, continuous: Bool, synchronizedStart: Double? = nil) throws {
        guard synchronizedStart?.isFinite ?? true else { throw MacroError.message("Invalid synchronized start time.") }
        guard !busy else { return }; guard CGPreflightPostEventAccess() else { throw MacroError.message("Allow Accessibility for playback, then fully quit and reopen TinyTask.") }; try enableMonitor(); try document.validate()
        guard !document.actions.isEmpty, speed.isFinite, (0.01...1000).contains(speed), (1...1_000_000).contains(loops) else { throw MacroError.message("Choose a nonempty macro, speed 0.01–1000x and loops 1–1,000,000.") }
        guard !document.actions.contains(where: { ["keyDown", "keyUp"].contains($0.type) && [recordKey, playKey, stopKey].contains($0.key) }) else { throw MacroError.message("This macro contains a control hotkey. Change the recording/playback hotkeys first.") }
        guard CGEventSource.flagsState(.combinedSessionState).intersection([.maskShift, .maskControl, .maskAlternate, .maskCommand]).isEmpty else { throw MacroError.message("Release modifier keys before playback.") }
        if preparedDocument != document || preparedSpeed != speed || preparedDisplays != displayLayout { try prepare(document, speed: speed) }
        macro = document; self.speed = speed; self.loops = loops; self.continuous = continuous; index = 0; loop = 0
        try resetPackets(document)
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
        guard original.isFinite, (original / speed).isFinite else { throw MacroError.message("Recording timeline is too large.") }
        for action in document.actions where ["move", "mouseDown", "mouseUp", "scroll"].contains(action.type) {
            guard displayLayout.contains(where: { $0.contains(CGPoint(x: action.x, y: action.y)) }) else { throw MacroError.message("A macro position is outside the current display layout.") }
        }
        try resetPackets(document)
        offsets = nextOffsets; loopDuration = original / speed
        preparedDocument = document; preparedSpeed = speed; preparedDisplays = displayLayout
    }
    private func resetPackets(_ document: MacroDocument) throws {
        preparationHeld.removeAll(); nextPrepared = 0; packets.removeAll(keepingCapacity: true)
        for _ in 0..<min(512, document.actions.count) { packets.append(try prepareNext(document)) }
    }
    private func prepareNext(_ document: MacroDocument) throws -> [CGEvent] {
        let action = document.actions[nextPrepared]
        let result = try events(action, dragging: preparationHeld["mouse:\(action.button)"] != nil)
        if action.type == "mouseDown" { preparationHeld[id(action)] = action }
        if action.type == "mouseUp" { preparationHeld.removeValue(forKey: id(action)) }
        nextPrepared += 1; return result
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
                let slot = index % packets.count
                for event in packets[slot] { event.post(tap: .cghidEventTap) }; track(a)
                if nextPrepared < macro.actions.count { packets[slot] = try prepareNext(macro) }
                index += 1; burst += 1
                if index == macro.actions.count {
                    release(clear: true); loop += 1
                    if !continuous && loop >= loops { stop(); return }; index = 0; try resetPackets(macro)
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
    private func events(_ a: MacroAction, release: Bool = false, dragging: Bool? = nil) throws -> [CGEvent] {
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
                if a.type == "move" { type = !(dragging ?? (held["mouse:\(a.button)"] != nil)) ? .mouseMoved : a.button == 0 ? .leftMouseDragged : a.button == 1 ? .rightMouseDragged : .otherMouseDragged }
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
    static func testLongRecording() throws {
        var document = MacroDocument(name: "Seven minutes at 1000 Hz")
        for i in 0..<420_000 { document.actions.append(MacroAction(type: i % 1000 == 0 ? "mouseDown" : i % 1000 == 999 ? "mouseUp" : "move", delay: 0.001, x: Double(100 + i % 100), y: 100, button: 1)) }
        document.actions.append(MacroAction(type: "delay", delay: 0.5))
        let url = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString + ".ttmacro")
        defer { try? FileManager.default.removeItem(at: url) }
        let data = try document.data(); try MacroFiles.write(data, to: url)
        guard try MacroDocument.load(MacroFiles.read(url)) == document else { throw MacroError.message("Long macro round trip changed events") }
        var limited = false
        do { _ = try MacroFiles.read(url, byteBudget: 1024) } catch { limited = true }
        guard limited else { throw MacroError.message("Compressed expansion budget ignored") }
        let engine = ClassicEngine(); try engine.prepare(document, speed: 1)
        guard engine.preparedPacketCapacity == 512 else { throw MacroError.message("Prepared event cache grew beyond 512") }
        // Check the entire native ring twice without posting a single event.
        for _ in 0..<2 {
            try engine.resetPackets(document)
            for i in document.actions.indices {
                let slot = i % engine.packets.count
                if document.actions[i].type == "move" {
                    guard engine.packets[slot].last?.type == .rightMouseDragged, engine.packets[slot].last?.location.x == CGFloat(document.actions[i].x) else { throw MacroError.message("Look-ahead lost drag type or point") }
                }
                if engine.nextPrepared < document.actions.count { engine.packets[slot] = try engine.prepareNext(document) }
            }
        }
        let bytes = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        fputs("Long recording verified: \(document.actions.count) events, JSON \(data.count) bytes, compressed \(bytes) bytes; native cache 512; two replays; no input posted.\n", stderr)
    }
    static func testPreciseRecording() throws {
        let engine = ClassicEngine(); engine.state = "Recording"; engine.recordStart = ProcessInfo.processInfo.systemUptime
        for i in 0..<5 {
            guard let event = CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: CGPoint(x: CGFloat(100 + i), y: 100), mouseButton: .left) else { throw MacroError.message("Could not create movement fixture") }
            _ = engine.receive(.mouseMoved, event)
        }
        guard engine.recording.actions.count == 5, engine.recording.actions.last?.x == 104 else { throw MacroError.message("Fine movement was discarded") }
        engine.state = "Ready"
    }
    func shutdown() { stop(); if let source = tapSource { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }; if let tap { CFMachPortInvalidate(tap) }; tap = nil; tapSource = nil }
}

// macOS ships gzip. File handles avoid shell quoting, pipe deadlocks and extra runtimes.
enum MacroFiles {
    private static func importByteBudget() -> Int {
        var statistics = vm_statistics64_data_t()
        var count = mach_msg_type_number_t(MemoryLayout<vm_statistics64_data_t>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &statistics) { pointer in
            pointer.withMemoryRebound(to: integer_t.self, capacity: Int(count)) { host_statistics64(mach_host_self(), HOST_VM_INFO64, $0, &count) }
        }
        let available = result == KERN_SUCCESS ? (UInt64(statistics.free_count) + UInt64(statistics.inactive_count)) * UInt64(vm_kernel_page_size) : ProcessInfo.processInfo.physicalMemory / 2
        return Int(min(UInt64(Int.max), available / 8))
    }
    static func read(_ url: URL, byteBudget: Int? = nil) throws -> Data {
        let handle = try FileHandle(forReadingFrom: url); defer { try? handle.close() }
        let header = try handle.read(upToCount: 2)
        let budget = byteBudget ?? importByteBudget()
        if header == Data([0x1f, 0x8b]) { return try gzip(url, decompress: true, byteBudget: budget) }
        try handle.seek(toOffset: 0)
        var data = Data()
        while let chunk = try handle.read(upToCount: 65536), !chunk.isEmpty {
            guard chunk.count <= budget - data.count else { throw MacroError.message("Not enough available memory to open this macro safely. Close other apps and retry; your file is unchanged.") }
            data.append(chunk)
        }
        return data
    }
    static func write(_ data: Data, to url: URL) throws {
        if url.pathExtension.lowercased() != "ttmacro" { try data.write(to: url, options: .atomic); return }
        let input = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: input) }
        try data.write(to: input, options: .atomic)
        try gzip(input, decompress: false).write(to: url, options: .atomic)
    }
    private static func gzip(_ input: URL, decompress: Bool, byteBudget: Int = Int.max) throws -> Data {
        let output = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: output) }
        guard FileManager.default.createFile(atPath: output.path, contents: nil) else { throw MacroError.message("Could not create macro temporary file.") }
        let source = try FileHandle(forReadingFrom: input), destination = try FileHandle(forWritingTo: output)
        defer { try? source.close(); try? destination.close() }
        let process = Process(); process.executableURL = URL(fileURLWithPath: "/usr/bin/gzip"); process.arguments = decompress ? ["-d", "-c"] : ["-1", "-c"]
        let pipe = Pipe(); process.standardInput = source; process.standardOutput = pipe; process.standardError = FileHandle.nullDevice
        try process.run()
        defer { try? pipe.fileHandleForReading.close(); if process.isRunning { process.terminate(); process.waitUntilExit() } }
        var produced = 0
        while let chunk = try pipe.fileHandleForReading.read(upToCount: 65536), !chunk.isEmpty {
            guard chunk.count <= byteBudget - produced else { throw MacroError.message("Not enough available memory to open this macro safely. Close other apps and retry; your file is unchanged.") }
            produced += chunk.count; try destination.write(contentsOf: chunk)
        }
        process.waitUntilExit()
        guard process.terminationStatus == 0 else { throw MacroError.message("Could not read or write the compressed macro. Check the file and available disk space.") }
        return try Data(contentsOf: output)
    }
}
