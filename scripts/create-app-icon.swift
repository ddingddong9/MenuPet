#!/usr/bin/env swift

import AppKit
import CoreGraphics
import Foundation

let outputURL: URL
let iconText: String

if CommandLine.arguments.count > 1 {
    outputURL = URL(fileURLWithPath: CommandLine.arguments[1])
} else {
    outputURL = URL(fileURLWithPath: "AppIcon.iconset")
}

if CommandLine.arguments.count > 2 {
    iconText = CommandLine.arguments[2]
} else {
    iconText = ProcessInfo.processInfo.environment["ICON_TEXT"] ?? "MenuPet"
}

let fileManager = FileManager.default
try? fileManager.removeItem(at: outputURL)
try fileManager.createDirectory(at: outputURL, withIntermediateDirectories: true)

let canvasSize = NSSize(width: 1024, height: 1024)
let text = iconText

func fittedFont() -> NSFont {
    for pointSize in stride(from: CGFloat(242), through: CGFloat(96), by: CGFloat(-2)) {
        let font = NSFont.systemFont(ofSize: pointSize, weight: .black)
        let attributes: [NSAttributedString.Key: Any] = [
            .font: font,
            .kern: -2.0
        ]
        let size = NSAttributedString(string: text, attributes: attributes).size()
        if size.width <= 860 && size.height <= 300 {
            return font
        }
    }

    return NSFont.systemFont(ofSize: 150, weight: .black)
}

func makeBaseIcon() -> NSImage {
    let image = NSImage(size: canvasSize)
    let font = fittedFont()
    let attributes: [NSAttributedString.Key: Any] = [
        .font: font,
        .kern: -2.0,
        .foregroundColor: NSColor.white
    ]
    let attributedText = NSAttributedString(string: text, attributes: attributes)
    let textSize = attributedText.size()
    let textRect = NSRect(
        x: (canvasSize.width - textSize.width) / 2,
        y: (canvasSize.height - textSize.height) / 2 + 16,
        width: textSize.width,
        height: textSize.height
    )

    let mask = NSImage(size: canvasSize)
    mask.lockFocus()
    NSColor.clear.setFill()
    NSRect(origin: .zero, size: canvasSize).fill()
    attributedText.draw(in: textRect)
    mask.unlockFocus()

    image.lockFocus()
    NSColor.white.setFill()
    NSRect(origin: .zero, size: canvasSize).fill()

    guard let context = NSGraphicsContext.current?.cgContext,
          let maskImage = mask.cgImage(forProposedRect: nil, context: nil, hints: nil),
          let gradient = CGGradient(
            colorsSpace: CGColorSpaceCreateDeviceRGB(),
            colors: [
                NSColor(calibratedRed: 1.0, green: 0.16, blue: 0.52, alpha: 1.0).cgColor,
                NSColor(calibratedRed: 1.0, green: 0.47, blue: 0.76, alpha: 1.0).cgColor,
                NSColor(calibratedRed: 1.0, green: 0.68, blue: 0.86, alpha: 1.0).cgColor
            ] as CFArray,
            locations: [0.0, 0.55, 1.0]
          ) else {
        NSColor.systemPink.setFill()
        attributedText.draw(in: textRect)
        image.unlockFocus()
        return image
    }

    context.saveGState()
    context.clip(to: CGRect(origin: .zero, size: canvasSize), mask: maskImage)
    context.drawLinearGradient(
        gradient,
        start: CGPoint(x: 96, y: 280),
        end: CGPoint(x: 928, y: 760),
        options: []
    )
    context.restoreGState()
    image.unlockFocus()

    return image
}

func writePNG(from image: NSImage, pixelSize: Int, name: String) throws {
    guard let bitmap = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: pixelSize,
        pixelsHigh: pixelSize,
        bitsPerSample: 8,
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 32
    ),
    let context = NSGraphicsContext(bitmapImageRep: bitmap) else {
        throw NSError(domain: "MenuPetIcon", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not create bitmap for \(name)"])
    }

    bitmap.size = NSSize(width: pixelSize, height: pixelSize)
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = context
    context.imageInterpolation = .high
    image.draw(
        in: NSRect(x: 0, y: 0, width: pixelSize, height: pixelSize),
        from: NSRect(origin: .zero, size: image.size),
        operation: .copy,
        fraction: 1.0
    )
    NSGraphicsContext.restoreGraphicsState()

    guard let png = bitmap.representation(using: .png, properties: [:]) else {
        throw NSError(domain: "MenuPetIcon", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not render \(name)"])
    }

    try png.write(to: outputURL.appendingPathComponent(name))
}

let baseIcon = makeBaseIcon()
let iconFiles: [(Int, String)] = [
    (16, "icon_16x16.png"),
    (32, "icon_16x16@2x.png"),
    (32, "icon_32x32.png"),
    (64, "icon_32x32@2x.png"),
    (128, "icon_128x128.png"),
    (256, "icon_128x128@2x.png"),
    (256, "icon_256x256.png"),
    (512, "icon_256x256@2x.png"),
    (512, "icon_512x512.png"),
    (1024, "icon_512x512@2x.png")
]

for (size, name) in iconFiles {
    try writePNG(from: baseIcon, pixelSize: size, name: name)
}
