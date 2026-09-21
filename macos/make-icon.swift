import AppKit
let output = CommandLine.arguments[1]
let image = NSImage(size: NSSize(width: 1024, height: 1024))
image.lockFocus()
NSColor(calibratedRed: 0.20, green: 0.37, blue: 0.81, alpha: 1).setFill()
NSBezierPath(roundedRect: NSRect(x: 20, y: 20, width: 984, height: 984), xRadius: 205, yRadius: 205).fill()
NSColor.white.setFill()
let pointer = NSBezierPath(); pointer.move(to: NSPoint(x: 295, y: 795)); pointer.line(to: NSPoint(x: 295, y: 260)); pointer.line(to: NSPoint(x: 440, y: 395)); pointer.line(to: NSPoint(x: 550, y: 180)); pointer.line(to: NSPoint(x: 655, y: 235)); pointer.line(to: NSPoint(x: 550, y: 445)); pointer.line(to: NSPoint(x: 745, y: 445)); pointer.close(); pointer.fill()
NSColor(calibratedRed: 1, green: 0.36, blue: 0.39, alpha: 1).setFill(); NSBezierPath(ovalIn: NSRect(x: 700, y: 700, width: 145, height: 145)).fill()
image.unlockFocus()
let bitmap = NSBitmapImageRep(data: image.tiffRepresentation!)!
try bitmap.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: output))
