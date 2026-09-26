import Foundation
import CryptoKit
import Security

// Protocol 1 is shared with the Windows client. All state/callbacks use the main queue.
final class MacSessionConnection {
    static var now: Double { ProcessInfo.processInfo.systemUptime }
    var message: (([String: Any]) -> Void)?
    var disconnected: ((String) -> Void)?
    private var socket: URLSessionWebSocketTask?
    private var queue: [[String: Any]] = []
    private var sending = false
    private var timer: Timer?
    private var sequence = 0
    private var stateSequence = 0
    private var lastReceived = now
    private var samples: [(Double, Double)] = []
    private(set) var room: String?
    private(set) var offset = 0.0, ping = 0.0, jitter = 0.0
    private(set) var synced = false
    var connected: Bool { socket != nil }
    static func credential(authority: String, secret: Data) -> String {
        Data(HMAC<SHA256>.authenticationCode(for: Data(authority.lowercased().utf8), using: SymmetricKey(data: secret))).base64EncodedString()
    }
    static func installationSecret() throws -> Data {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: "TinyTask2.Sessions", kSecAttrAccount as String: "installation", kSecReturnData as String: true]
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        if status == errSecSuccess, let data = result as? Data, data.count == 32 { return data }
        guard status == errSecItemNotFound else { throw NSError(domain: NSOSStatusErrorDomain, code: Int(status)) }
        var bytes = [UInt8](repeating: 0, count: 32)
        guard SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes) == errSecSuccess else { throw MacroError.message("Could not create the installation credential.") }
        var insert = query; insert.removeValue(forKey: kSecReturnData as String); insert[kSecValueData as String] = Data(bytes); insert[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        let added = SecItemAdd(insert as CFDictionary, nil)
        guard added == errSecSuccess else { throw NSError(domain: NSOSStatusErrorDomain, code: Int(added)) }
        return Data(bytes)
    }
    func connect(endpoint: String, id: String, username: String, publicID: String, secret: Data) throws {
        guard socket == nil else { throw MacroError.message("Disconnect before changing servers.") }
        guard let url = URL(string: endpoint), let host = url.host, url.path == "/session", url.user == nil, url.password == nil,
              url.scheme == "wss" || (url.scheme == "ws" && ["localhost", "127.0.0.1", "::1"].contains(host)) else { throw MacroError.message("Use a secure wss:// server ending in /session. Local tests may use ws://localhost.") }
        guard !username.trimmingCharacters(in: .whitespaces).isEmpty else { throw MacroError.message("Choose a username first.") }
        let defaultPort = url.scheme == "wss" ? 443 : 80
        let port = url.port.flatMap { $0 == defaultPort ? nil : ":\($0)" } ?? ""
        let authority = "\(url.scheme!)://\(host.contains(":") ? "[\(host)]" : host)\(port)"
        let next = URLSession.shared.webSocketTask(with: url); next.maximumMessageSize = 16384
        socket = next; next.resume(); lastReceived = Self.now; receive(next)
        enqueue(["type": "hello", "protocol": 1, "deviceId": id, "secret": Self.credential(authority: authority, secret: secret), "username": username, "publicId": publicID])
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            guard let self else { return }
            if Self.now - self.lastReceived > 15 { self.fail("Connection timed out. Local playback stopped."); return }
            self.enqueue(["type": "ping", "sent": Self.now])
        }
        enqueue(["type": "ping", "sent": Self.now])
    }
    func command(_ type: String, _ fields: [String: Any] = [:]) {
        guard socket != nil else { return }
        var packet = fields; sequence += 1
        packet["type"] = type; packet["requestId"] = UUID().uuidString; packet["commandSequence"] = sequence
        if let room { packet["sessionId"] = room }; enqueue(packet)
    }
    private func enqueue(_ value: [String: Any]) {
        guard queue.count < 128 else { fail("Session send queue is full."); return }
        queue.append(value); flush()
    }
    private func flush() {
        guard !sending, let current = socket, !queue.isEmpty else { return }
        do {
            let data = try JSONSerialization.data(withJSONObject: queue.removeFirst()); sending = true
            current.send(.string(String(decoding: data, as: UTF8.self))) { [weak self, weak current] error in DispatchQueue.main.async {
                guard let self, let current, self.socket === current else { return }
                self.sending = false
                if let error { self.fail(error.localizedDescription) } else { self.flush() }
            } }
        } catch { fail(error.localizedDescription) }
    }
    private func receive(_ current: URLSessionWebSocketTask) {
        current.receive { [weak self, weak current] result in DispatchQueue.main.async {
            guard let self, let current, self.socket === current else { return }
            do {
                let data: Data
                switch try result.get() { case .data(let d): data = d; case .string(let s): data = Data(s.utf8); @unknown default: throw MacroError.message("Unknown session frame.") }
                guard let packet = try JSONSerialization.jsonObject(with: data) as? [String: Any], let type = packet["type"] as? String else { throw MacroError.message("Invalid session message.") }
                self.lastReceived = Self.now
                if type == "pong", let sent = packet["echo"] as? Double, let server = packet["serverTime"] as? Double {
                    let rtt = Self.now - sent
                    guard rtt.isFinite, (0...10).contains(rtt), server.isFinite else { throw MacroError.message("Invalid clock sample.") }
                    self.samples.append((rtt, server - (sent + Self.now) / 2)); if self.samples.count > 16 { self.samples.removeFirst() }
                    self.offset = self.samples.min { $0.0 < $1.0 }!.1; self.ping = rtt * 1000
                    self.jitter = (self.samples.map { $0.0 }.max()! - self.samples.map { $0.0 }.min()!) * 1000
                    self.synced = self.samples.count >= 3 && rtt <= 0.2 && self.jitter <= 50
                    self.command("quality", ["rtt": rtt, "jitter": self.jitter / 1000, "synced": self.synced])
                }
                var deliver = true
                if ["state", "start", "stop"].contains(type) {
                    guard let id = packet["sessionId"] as? String, let seq = packet["sequence"] as? Int else { throw MacroError.message("Invalid session sequence.") }
                    deliver = (self.room == nil ? type == "state" : self.room?.lowercased() == id.lowercased()) && seq > self.stateSequence
                    if deliver { self.room = id; self.stateSequence = seq }
                } else if type == "left" { self.room = nil; self.stateSequence = 0 }
                if deliver { self.message?(packet) }; if self.socket === current { self.receive(current) }
            } catch { self.fail(error.localizedDescription) }
        } }
    }
    private func fail(_ reason: String) { disconnect(); disconnected?(reason) }
    func disconnect() {
        timer?.invalidate(); timer = nil; socket?.cancel(with: .goingAway, reason: nil); socket = nil
        queue.removeAll(); sending = false; synced = false; samples.removeAll(); room = nil; sequence = 0; stateSequence = 0
    }
}
