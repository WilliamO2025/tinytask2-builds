import Foundation

@main struct MacSessionChecks {
    static func main() {
        let client = MacSessionConnection(); var done = false; var failure: String?; var invited = false; var started = false
        let secret = Data(repeating: 42, count: 32)
        precondition(MacSessionConnection.credential(authority: "wss://one.example", secret: secret) != MacSessionConnection.credential(authority: "wss://two.example", secret: secret))
        client.disconnected = { failure = $0; done = true }
        client.message = { packet in
            let type = packet["type"] as? String ?? ""
            print("Mac received \(type), synced=\(client.synced), ping=\(client.ping), jitter=\(client.jitter)")
            if type == "error" { failure = packet["message"] as? String; done = true }
            if type == "pong", client.synced, !invited { invited = true; client.command("invite", ["publicId": "WINDOWS-TEST"]) }
            if type == "state", let members = packet["members"] as? [[String: Any]], members.count == 2 {
                if packet["reason"] as? String == "joined" { client.command("ready", ["ready": true, "prepared": true, "task": "Mac fixture (no input)"]) }
                if members.allSatisfy({ $0["ready"] as? Bool == true }), !started { started = true; client.command("start", ["delay": 0.3]) }
            }
            if type == "start" {
                guard let target = packet["target"] as? Double, target - client.offset > MacSessionConnection.now, (packet["participants"] as? [String])?.count == 2 else { failure = "Invalid shared target"; done = true; return }
                print("PASS Mac client approved invitation, Ready, clock conversion and common start")
                client.command("finished", ["runSequence": packet["sequence"]!])
            }
            if type == "state", packet["reason"] as? String == "finished" { client.command("stop") }
            if type == "stop" { print("PASS Mac client receives authorized synchronized Stop"); done = true }
        }
        do { try client.connect(endpoint: "ws://127.0.0.1:18763/session", id: UUID().uuidString, username: "Mac test", publicID: "MAC-TEST", secret: secret) } catch { failure = error.localizedDescription; done = true }
        let end = Date().addingTimeInterval(60)
        while !done && Date() < end { RunLoop.current.run(until: Date().addingTimeInterval(0.05)) }
        client.disconnect()
        if let failure { fputs(failure + "\n", stderr); exit(1) }
        guard done else { fputs("Session integration timed out\n", stderr); exit(1) }
        print("PASS Mac session protocol integration; no physical input sent")
    }
}
