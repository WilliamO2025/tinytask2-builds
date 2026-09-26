import AppKit

private final class SessionStack: NSStackView { override var isFlipped: Bool { true } }

final class MacSessionWindow: NSWindowController, NSWindowDelegate {
    private unowned let app: TinyTaskApp
    private let client = MacSessionConnection()
    private let username = NSTextField(), publicID = NSTextField(), endpoint = NSTextField(), inviteID = NSTextField(), code = NSTextField()
    private let status = NSTextField(wrappingLabelWithString: "Connect to the same server as your Windows or Mac friends. No public server is configured.")
    private let members = NSTextField(wrappingLabelWithString: "No session")
    private let invitation = NSPopUpButton()
    private let delay = NSTextField(string: "0")
    private let lock = NSButton(checkboxWithTitle: "Lock session", target: nil, action: nil)
    private let everyone = NSButton(checkboxWithTitle: "Everyone can start/stop", target: nil, action: nil)
    private let allReady = NSButton(checkboxWithTitle: "Require everyone Ready", target: nil, action: nil)
    private let defaults = UserDefaults.standard
    private let id: String
    private var invites: [String] = []
    private var prepared: (MacroDocument, Double, Int, Bool)?
    private var activeRun: Int?
    private var activeRoom: String?
    private var starting = false
    init(app: TinyTaskApp) {
        self.app = app; id = UserDefaults.standard.string(forKey: "sessionDeviceID") ?? UUID().uuidString
        super.init(window: NSWindow(contentRect: NSRect(x: 0, y: 0, width: 620, height: 640), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false))
        defaults.set(id, forKey: "sessionDeviceID"); window!.title = "TinyTask sessions"; window!.delegate = self; window!.center()
        let scroll = NSScrollView(); scroll.hasVerticalScroller = true; window!.contentView = scroll
        let stack = SessionStack(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 10; stack.edgeInsets = NSEdgeInsets(top: 20, left: 20, bottom: 20, right: 20)
        scroll.documentView = stack; stack.translatesAutoresizingMaskIntoConstraints = false
        stack.widthAnchor.constraint(equalTo: scroll.contentView.widthAnchor).isActive = true
        func field(_ title: String, _ field: NSTextField, _ text: String) { stack.addArrangedSubview(NSTextField(labelWithString: title)); field.stringValue = text; field.widthAnchor.constraint(equalToConstant: 555).isActive = true; stack.addArrangedSubview(field) }
        func row(_ items: [(String, Selector)]) { stack.addArrangedSubview(NSStackView(views: items.map { NSButton(title: $0.0, target: self, action: $0.1) })) }
        field("Username (the name others see)", username, defaults.string(forKey: "sessionUsername") ?? "")
        field("Public ID", publicID, defaults.string(forKey: "sessionPublicID") ?? "TT-" + String(UUID().uuidString.replacingOccurrences(of: "-", with: "").prefix(12)))
        field("Session server", endpoint, defaults.string(forKey: "sessionEndpoint") ?? "wss://")
        row([("Connect", #selector(connect)), ("Disconnect", #selector(disconnectAction)), ("Create session", #selector(create)), ("Leave", #selector(leave))])
        stack.addArrangedSubview(status); status.preferredMaxLayoutWidth = 555
        field("Invite by Public ID", inviteID, ""); row([("Invite", #selector(invite))])
        field("Join with code", code, ""); row([("Request to join", #selector(join))])
        stack.addArrangedSubview(invitation); row([("Accept", #selector(accept)), ("Decline", #selector(decline))])
        stack.addArrangedSubview(members); members.preferredMaxLayoutWidth = 555
        allReady.state = .on
        for control in [lock, everyone, allReady] { stack.addArrangedSubview(control) }
        row([("Apply host settings", #selector(settings))])
        row([("Prepare / Ready", #selector(ready)), ("Not Ready", #selector(notReady)), ("Start together", #selector(start)), ("Stop session", #selector(stopSession))])
        field("Optional start delay in seconds (0 = Fast Start)", delay, "0")
        client.message = { [weak self] in self?.receive($0) }
        client.disconnected = { [weak self] reason in self?.disconnect(); self?.status.stringValue = reason }
    }
    required init?(coder: NSCoder) { fatalError("Not supported") }
    private func confirm(_ text: String) -> Bool { let alert = NSAlert(); alert.messageText = text; alert.addButton(withTitle: "Continue"); alert.addButton(withTitle: "Cancel"); return alert.runModal() == .alertFirstButtonReturn }
    @objc private func connect() {
        do { try client.connect(endpoint: endpoint.stringValue.trimmingCharacters(in: .whitespaces), id: id, username: username.stringValue, publicID: publicID.stringValue.uppercased(), secret: MacSessionConnection.installationSecret()); status.stringValue = "Connecting..." }
        catch { status.stringValue = error.localizedDescription }
    }
    @objc private func disconnectAction() { disconnect() }
    func disconnect() { prepared = nil; activeRun = nil; activeRoom = nil; client.disconnect(); app.engine.stop(); members.stringValue = "No session"; invitation.removeAllItems(); invites.removeAll(); status.stringValue = "Disconnected" }
    func windowWillClose(_ notification: Notification) { disconnect() }
    @objc private func create() { client.command("create") }
    @objc private func leave() { unready(); app.engine.stop(); client.command("leave") }
    @objc private func invite() { if confirm("Invite \(inviteID.stringValue)?") { client.command("invite", ["publicId": inviteID.stringValue.uppercased()]) } }
    @objc private func join() { client.command("join", ["code": code.stringValue.trimmingCharacters(in: .whitespaces)]) }
    private func reply(_ type: String) { let index = invitation.indexOfSelectedItem; guard invites.indices.contains(index) else { return }; client.command(type, ["inviteId": invites[index]]); invites.remove(at: index); invitation.removeItem(at: index) }
    @objc private func accept() { reply("accept") }
    @objc private func decline() { reply("decline") }
    @objc private func settings() { client.command("settings", ["locked": lock.state == .on, "everyone": everyone.state == .on, "requireReady": allReady.state == .on, "cancelIfSlow": false]) }
    @objc private func ready() {
        guard client.room != nil, client.synced else { status.stringValue = "Join a session and wait for stable clock samples."; return }
        guard !app.engine.busy, let macro = app.macro, macro.actions.contains(where: { $0.type != "delay" }), let speed = Double(app.speed.stringValue), speed.isFinite, (0.01...1000).contains(speed), let loops = Int(app.loops.stringValue), (1...1_000_000).contains(loops) else { status.stringValue = "Open a recording and choose valid speed/loop settings first."; return }
        guard confirm("Allow the Host to start your local recording? It controls this Mac's mouse and keyboard. F10 stops locally."), app.prepareInput() else { return }
        do { try app.engine.prepare(macro, speed: speed); prepared = (macro, speed, loops, app.continuous.state == .on); client.command("ready", ["ready": true, "prepared": true, "task": String(macro.name.prefix(64))]); status.stringValue = "Ready - clock sampled" } catch { status.stringValue = error.localizedDescription }
    }
    func unready() { guard prepared != nil else { return }; prepared = nil; client.command("ready", ["ready": false, "prepared": false, "task": "No prepared task"]) }
    @objc private func notReady() { unready(); app.engine.stop() }
    @objc private func start() { guard let seconds = Double(delay.stringValue), seconds.isFinite, (0...3600).contains(seconds) else { status.stringValue = "Enter a delay between 0 and 3600 seconds."; return }; client.command("start", ["delay": seconds]) }
    @objc private func stopSession() { unready(); app.engine.stop(); client.command("stop") }
    func engineChanged() {
        if app.engine.busy && !starting && activeRun == nil { unready() }
        if app.engine.state == "Ready", let run = activeRun { activeRun = nil; if client.room == activeRoom { client.command("finished", ["runSequence": run]) }; activeRoom = nil }
    }
    private func receive(_ packet: [String: Any]) {
        guard let type = packet["type"] as? String else { return }
        switch type {
        case "welcome":
            defaults.set(username.stringValue, forKey: "sessionUsername"); defaults.set(publicID.stringValue.uppercased(), forKey: "sessionPublicID"); defaults.set(endpoint.stringValue, forKey: "sessionEndpoint"); status.stringValue = "Connected. Create a session or invite someone."
        case "error": status.stringValue = packet["message"] as? String ?? "Session error"
        case "pong": status.stringValue = String(format: "Ping %.0f ms - jitter %.0f ms - %@", client.ping, client.jitter, client.synced ? "clock sampled" : "sampling")
        case "invitation": if let invite = packet["inviteId"] as? String { invites.append(invite); invitation.addItem(withTitle: "\(packet["from"] as? String ?? "User") (\(packet["publicId"] as? String ?? ""))") }
        case "state":
            let host = packet["host"] as? String ?? ""; let rows = packet["members"] as? [[String: Any]] ?? []
            members.stringValue = "Code: \(packet["code"] as? String ?? "")\n" + rows.map { "\($0["username"] as? String ?? "User") - \(($0["id"] as? String) == host ? "Host" : "Member") - \(($0["ready"] as? Bool) == true ? "Ready" : "Not Ready") - \($0["task"] as? String ?? "")" }.joined(separator: "\n")
            lock.state = packet["locked"] as? Bool == true ? .on : .off; everyone.state = packet["everyone"] as? Bool == true ? .on : .off; allReady.state = packet["requireReady"] as? Bool == true ? .on : .off
        case "start":
            let participants = packet["participants"] as? [String] ?? []; let run = packet["sequence"] as? Int ?? 0
            guard participants.contains(where: { $0.lowercased() == id.lowercased() }) else { unready(); return }
            guard let task = prepared, client.synced, let serverTarget = packet["target"] as? Double, serverTarget.isFinite, serverTarget - client.offset >= MacSessionConnection.now - 0.05, !app.engine.busy else { unready(); client.command("finished", ["runSequence": run]); status.stringValue = "Start rejected: task or clock is not ready, or the command arrived too late."; return }
            prepared = nil; activeRun = run; activeRoom = client.room; starting = true
            defer { starting = false }
            do { try app.engine.play(task.0, speed: task.1, loops: task.2, continuous: task.3, synchronizedStart: serverTarget - client.offset) }
            catch { activeRun = nil; activeRoom = nil; client.command("finished", ["runSequence": run]); status.stringValue = error.localizedDescription }
        case "stop", "left", "excluded": unready(); app.engine.stop(); if type == "left" { members.stringValue = "No session" }
        default: break
        }
    }
}
