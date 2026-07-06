import Cocoa
import QuartzCore
import UniformTypeIdentifiers

private let imageResourceName = "pet-character"
private var appDisplayName: String {
    Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String
        ?? Bundle.main.object(forInfoDictionaryKey: "CFBundleName") as? String
        ?? "MenuPet"
}
private let savedScaleKey = "MenuPetScalePercent"
private let savedMotionModeKey = "MenuPetMotionMode"
private let defaultScalePercent = 100.0
private let maxPetHeight: CGFloat = 360.0
private let petMargin: CGFloat = 24.0
private var speechBubbleText: String {
    Bundle.main.object(forInfoDictionaryKey: "PetSpeechText") as? String ?? "hug me..."
}
private let speechBubbleImageResourceName = "speech-message"
private let speechBubbleSize = NSSize(width: 220.0, height: 70.0)

private enum MotionMode: Int, CaseIterable {
    case wander
    case bounce
    case rest

    var title: String {
        switch self {
        case .wander:
            return "랜덤 산책"
        case .bounce:
            return "제자리 통통"
        case .rest:
            return "잠깐 쉬기"
        }
    }
}

final class ClickableImageView: NSImageView {
    var onClick: (() -> Void)?
    var facesRight = false {
        didSet {
            needsDisplay = true
        }
    }

    override var acceptsFirstResponder: Bool { true }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool {
        true
    }

    override func mouseDown(with event: NSEvent) {
        onClick?()
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let image else {
            super.draw(dirtyRect)
            return
        }

        let imageSize = image.size
        let widthRatio = bounds.width / max(imageSize.width, 1.0)
        let heightRatio = bounds.height / max(imageSize.height, 1.0)
        let ratio = min(widthRatio, heightRatio)
        let drawSize = NSSize(width: imageSize.width * ratio, height: imageSize.height * ratio)
        let drawRect = NSRect(
            x: (bounds.width - drawSize.width) / 2,
            y: (bounds.height - drawSize.height) / 2,
            width: drawSize.width,
            height: drawSize.height
        )

        guard let context = NSGraphicsContext.current?.cgContext else {
            image.draw(in: drawRect)
            return
        }

        context.saveGState()
        if facesRight {
            context.translateBy(x: bounds.midX, y: bounds.midY)
            context.scaleBy(x: -1.0, y: 1.0)
            context.translateBy(x: -bounds.midX, y: -bounds.midY)
        }

        image.draw(
            in: drawRect,
            from: NSRect(origin: .zero, size: imageSize),
            operation: .sourceOver,
            fraction: 1.0,
            respectFlipped: true,
            hints: [.interpolation: NSImageInterpolation.high]
        )
        context.restoreGState()
    }
}

final class PetPanel: NSPanel {
    init(contentRect: NSRect) {
        super.init(
            contentRect: contentRect,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        hidesOnDeactivate = false
        isMovable = false
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        ignoresMouseEvents = false
    }

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class SpeechBubblePanel: NSPanel {
    init(contentRect: NSRect) {
        super.init(
            contentRect: contentRect,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        hidesOnDeactivate = false
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        ignoresMouseEvents = true
    }

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class SpeechBubbleView: NSView {
    var message = speechBubbleText {
        didSet {
            needsDisplay = true
        }
    }
    var messageImage: NSImage? {
        didSet {
            needsDisplay = true
        }
    }

    override var isOpaque: Bool { false }

    override func draw(_ dirtyRect: NSRect) {
        let bodyRect = bounds.insetBy(dx: 7, dy: 12).offsetBy(dx: 0, dy: 5)
        let bubblePath = makeSoftBubblePath(in: bodyRect)

        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow()
        shadow.shadowColor = NSColor.systemPink.withAlphaComponent(0.20)
        shadow.shadowBlurRadius = 8
        shadow.shadowOffset = NSSize(width: 0, height: -2)
        shadow.set()
        NSColor(calibratedRed: 1.0, green: 0.985, blue: 0.995, alpha: 1.0).setFill()
        bubblePath.fill()
        NSGraphicsContext.restoreGraphicsState()

        NSColor(calibratedRed: 1.0, green: 0.58, blue: 0.76, alpha: 0.88).setStroke()
        bubblePath.lineWidth = 1.6
        bubblePath.stroke()

        drawBlush(in: bodyRect)

        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = .center

        if let messageImage {
            let imageRect = aspectFitRect(for: messageImage.size, in: bodyRect.insetBy(dx: 13, dy: 3))
            messageImage.draw(
                in: imageRect,
                from: NSRect(origin: .zero, size: messageImage.size),
                operation: .sourceOver,
                fraction: 1.0,
                respectFlipped: true,
                hints: [.interpolation: NSImageInterpolation.high]
            )
            return
        }

        let font = NSFont(name: "NanumPenScript", size: 27)
            ?? NSFont(name: "AppleSDGothicNeo-Heavy", size: 23)
            ?? NSFont.boldSystemFont(ofSize: 22)

        let attributes: [NSAttributedString.Key: Any] = [
            .font: font,
            .foregroundColor: NSColor.black,
            .paragraphStyle: paragraph
        ]
        let attributedMessage = NSAttributedString(string: message, attributes: attributes)
        let textSize = attributedMessage.size()
        let textRect = NSRect(
            x: bodyRect.minX + 10,
            y: bodyRect.midY - textSize.height / 2 - 1,
            width: bodyRect.width - 20,
            height: textSize.height + 4
        )
        attributedMessage.draw(in: textRect)
    }

    private func makeSoftBubblePath(in rect: NSRect) -> NSBezierPath {
        let path = NSBezierPath()
        let minX = rect.minX
        let maxX = rect.maxX
        let minY = rect.minY
        let maxY = rect.maxY
        let midX = rect.midX
        let midY = rect.midY
        let leftBulge: CGFloat = 36
        let rightBulge: CGFloat = 34

        path.move(to: NSPoint(x: minX + leftBulge, y: minY + 1))
        path.curve(
            to: NSPoint(x: minX + 16, y: midY - 4),
            controlPoint1: NSPoint(x: minX + 21, y: minY + 1),
            controlPoint2: NSPoint(x: minX + 10, y: minY + 19)
        )
        path.curve(
            to: NSPoint(x: minX + 43, y: maxY - 5),
            controlPoint1: NSPoint(x: minX + 20, y: maxY - 22),
            controlPoint2: NSPoint(x: minX + 18, y: maxY - 6)
        )
        path.curve(
            to: NSPoint(x: midX - 8, y: maxY - 2),
            controlPoint1: NSPoint(x: minX + 78, y: maxY + 7),
            controlPoint2: NSPoint(x: midX - 44, y: maxY + 4)
        )
        path.curve(
            to: NSPoint(x: maxX - 45, y: maxY - 5),
            controlPoint1: NSPoint(x: midX + 23, y: maxY + 5),
            controlPoint2: NSPoint(x: maxX - 67, y: maxY + 7)
        )
        path.curve(
            to: NSPoint(x: maxX - 12, y: midY - 2),
            controlPoint1: NSPoint(x: maxX - 19, y: maxY - 7),
            controlPoint2: NSPoint(x: maxX - 5, y: maxY - 25)
        )
        path.curve(
            to: NSPoint(x: maxX - rightBulge, y: minY + 3),
            controlPoint1: NSPoint(x: maxX - 17, y: minY + 22),
            controlPoint2: NSPoint(x: maxX - 19, y: minY + 6)
        )
        path.curve(
            to: NSPoint(x: midX + 18, y: minY + 2),
            controlPoint1: NSPoint(x: maxX - 78, y: minY - 7),
            controlPoint2: NSPoint(x: midX + 52, y: minY - 3)
        )

        path.line(to: NSPoint(x: midX + 9, y: 6))
        path.curve(
            to: NSPoint(x: midX - 24, y: minY + 6),
            controlPoint1: NSPoint(x: midX - 1, y: 0),
            controlPoint2: NSPoint(x: midX - 11, y: 2)
        )
        path.curve(
            to: NSPoint(x: minX + leftBulge, y: minY + 1),
            controlPoint1: NSPoint(x: midX - 55, y: minY - 2),
            controlPoint2: NSPoint(x: minX + 72, y: minY - 4)
        )
        path.close()
        return path
    }

    private func drawBlush(in rect: NSRect) {
        NSColor.systemPink.withAlphaComponent(0.16).setFill()
        NSBezierPath(ovalIn: NSRect(x: rect.minX + 16, y: rect.midY - 9, width: 9, height: 9)).fill()
        NSBezierPath(ovalIn: NSRect(x: rect.maxX - 25, y: rect.midY - 5, width: 7, height: 7)).fill()
    }

    private func aspectFitRect(for imageSize: NSSize, in bounds: NSRect) -> NSRect {
        let ratio = min(bounds.width / max(imageSize.width, 1.0), bounds.height / max(imageSize.height, 1.0))
        let drawSize = NSSize(width: imageSize.width * ratio, height: imageSize.height * ratio)
        return NSRect(
            x: bounds.midX - drawSize.width / 2,
            y: bounds.midY - drawSize.height / 2,
            width: drawSize.width,
            height: drawSize.height
        )
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private var petPanel: PetPanel?
    private var speechBubblePanel: SpeechBubblePanel?
    private var speechBubbleView: SpeechBubbleView?
    private var imageView: ClickableImageView?
    private var motionTimer: Timer?
    private var motionStart = Date()
    private var lastMotionTick = Date()
    private var restingFrame = NSRect.zero
    private var petPosition = NSPoint.zero
    private var walkTarget = NSPoint.zero
    private var nextDecisionDate = Date()
    private var restUntil: Date?
    private var speechBubbleUntil: Date?
    private var jumpStart: Date?
    private var dashUntil: Date?
    private var walkPhase: TimeInterval = 0
    private var walkSpeed: CGFloat = 110
    private var motionMenuItems: [MotionMode: NSMenuItem] = [:]

    private let scaleLabel = NSTextField(labelWithString: "")
    private let scaleSlider = NSSlider(value: defaultScalePercent, minValue: 0.0, maxValue: 100.0, target: nil, action: nil)
    private let fileManager = FileManager.default

    private var scalePercent: Double
    private var motionMode: MotionMode

    override init() {
        let saved = UserDefaults.standard.object(forKey: savedScaleKey) as? Double
        scalePercent = saved.map { min(100.0, max(0.0, $0.rounded())) } ?? defaultScalePercent
        let savedMode = UserDefaults.standard.integer(forKey: savedMotionModeKey)
        motionMode = MotionMode(rawValue: savedMode) ?? .wander
        super.init()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        configureStatusItem()
        updateScaleControls()

        if scalePercent > 0 {
            showPet(animateFromBottom: true)
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        stopMotion()
    }

    private func configureStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem = item

        if let button = item.button {
            button.image = makeWhiteHeartStatusImage()
            button.imagePosition = .imageOnly
            button.contentTintColor = .white
        }

        item.menu = makeMenu()
    }

    private func makeWhiteHeartStatusImage() -> NSImage? {
        guard let symbol = NSImage(
            systemSymbolName: "heart.fill",
            accessibilityDescription: appDisplayName
        )?.withSymbolConfiguration(NSImage.SymbolConfiguration(pointSize: 16, weight: .semibold)) else {
            return nil
        }

        let image = NSImage(size: NSSize(width: 18, height: 18))
        image.lockFocus()
        NSColor.clear.setFill()
        NSRect(origin: .zero, size: image.size).fill()
        symbol.draw(
            in: NSRect(x: 1, y: 1, width: 16, height: 16),
            from: .zero,
            operation: .sourceOver,
            fraction: 1.0
        )
        NSColor.white.setFill()
        NSRect(x: 1, y: 1, width: 16, height: 16).fill(using: .sourceAtop)
        image.unlockFocus()
        image.isTemplate = false
        return image
    }

    private func makeMenu() -> NSMenu {
        let menu = NSMenu()

        let choosePetItem = NSMenuItem(title: "펫 이미지 선택...", action: #selector(choosePetImage), keyEquivalent: "")
        choosePetItem.target = self
        menu.addItem(choosePetItem)

        let chooseSpeechItem = NSMenuItem(title: "말풍선 이미지 선택...", action: #selector(chooseSpeechImage), keyEquivalent: "")
        chooseSpeechItem.target = self
        menu.addItem(chooseSpeechItem)

        menu.addItem(.separator())

        let showItem = NSMenuItem(title: "\(appDisplayName) 보이기", action: #selector(showPetFromMenu), keyEquivalent: "")
        showItem.target = self
        menu.addItem(showItem)

        let hideItem = NSMenuItem(title: "\(appDisplayName) 숨기기", action: #selector(hidePetFromMenu), keyEquivalent: "")
        hideItem.target = self
        menu.addItem(hideItem)

        menu.addItem(.separator())
        menu.addItem(makeSliderMenuItem())
        menu.addItem(.separator())
        addMotionItems(to: menu)
        menu.addItem(.separator())

        let quitItem = NSMenuItem(title: "종료", action: #selector(quit), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        return menu
    }

    private func addMotionItems(to menu: NSMenu) {
        for mode in MotionMode.allCases {
            let item = NSMenuItem(title: mode.title, action: #selector(motionModeChanged(_:)), keyEquivalent: "")
            item.target = self
            item.tag = mode.rawValue
            motionMenuItems[mode] = item
            menu.addItem(item)
        }

        updateMotionMenuItems()
    }

    private func makeSliderMenuItem() -> NSMenuItem {
        let container = NSView(frame: NSRect(x: 0, y: 0, width: 240, height: 58))

        scaleLabel.frame = NSRect(x: 14, y: 32, width: 212, height: 18)
        scaleLabel.font = .systemFont(ofSize: 13)
        scaleLabel.textColor = .labelColor
        container.addSubview(scaleLabel)

        scaleSlider.frame = NSRect(x: 12, y: 8, width: 216, height: 24)
        scaleSlider.numberOfTickMarks = 6
        scaleSlider.allowsTickMarkValuesOnly = false
        scaleSlider.target = self
        scaleSlider.action = #selector(sliderChanged(_:))
        container.addSubview(scaleSlider)

        let item = NSMenuItem()
        item.view = container
        return item
    }

    private func updateScaleControls() {
        scaleLabel.stringValue = "\(appDisplayName) 크기 \(Int(scalePercent))%"
        scaleSlider.doubleValue = scalePercent
    }

    private func updateMotionMenuItems() {
        for (mode, item) in motionMenuItems {
            item.state = mode == motionMode ? .on : .off
        }
    }

    private func setScalePercent(_ newValue: Double) {
        scalePercent = min(100.0, max(0.0, newValue.rounded()))
        UserDefaults.standard.set(scalePercent, forKey: savedScaleKey)
        updateScaleControls()
        applyScaleChange()
    }

    private func applyScaleChange() {
        if scalePercent <= 0 {
            hidePetOnly()
        } else {
            if petPanel?.isVisible == true {
                resizePet()
            } else {
                showPet(animateFromBottom: true)
            }
        }
    }

    private func makePetPanelIfNeeded() -> PetPanel? {
        if let petPanel {
            return petPanel
        }

        guard let image = loadPetImage() else {
            NSLog("\(appDisplayName) 이미지 리소스를 찾을 수 없습니다: \(imageResourceName)")
            return nil
        }

        let frame = targetFrame(for: image)
        let panel = PetPanel(contentRect: frame)
        let imageView = ClickableImageView(frame: NSRect(origin: .zero, size: frame.size))
        imageView.image = image
        imageView.imageScaling = .scaleProportionallyUpOrDown
        imageView.autoresizingMask = [.width, .height]
        imageView.onClick = { [weak self] in
            self?.hidePetOnly()
        }

        panel.contentView = imageView
        self.petPanel = panel
        self.imageView = imageView
        return panel
    }

    private func makeSpeechBubblePanelIfNeeded() -> SpeechBubblePanel {
        if let speechBubblePanel {
            return speechBubblePanel
        }

        let panel = SpeechBubblePanel(contentRect: NSRect(origin: .zero, size: speechBubbleSize))
        let bubbleView = SpeechBubbleView(frame: NSRect(origin: .zero, size: speechBubbleSize))
        bubbleView.message = speechBubbleText
        bubbleView.messageImage = loadSpeechBubbleImage()
        bubbleView.autoresizingMask = [.width, .height]
        panel.contentView = bubbleView

        speechBubblePanel = panel
        speechBubbleView = bubbleView
        return panel
    }

    private var appSupportDirectoryURL: URL {
        let baseURL = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSTemporaryDirectory())
        let identifier = Bundle.main.bundleIdentifier ?? "dev.example.menupet"
        return baseURL.appendingPathComponent(identifier, isDirectory: true)
    }

    private var userPetImageURL: URL {
        appSupportDirectoryURL.appendingPathComponent("pet-character.png")
    }

    private var userSpeechBubbleImageURL: URL {
        appSupportDirectoryURL.appendingPathComponent("speech-message.png")
    }

    private func loadPetImage() -> NSImage? {
        if let image = NSImage(contentsOf: userPetImageURL) {
            return image
        }

        if let bundledURL = Bundle.main.url(forResource: imageResourceName, withExtension: "png"),
           let image = NSImage(contentsOf: bundledURL) {
            return image
        }

        return nil
    }

    private func loadSpeechBubbleImage() -> NSImage? {
        if let image = NSImage(contentsOf: userSpeechBubbleImageURL) {
            return image
        }

        if let bundledURL = Bundle.main.url(forResource: speechBubbleImageResourceName, withExtension: "png") {
            return NSImage(contentsOf: bundledURL)
        }

        return nil
    }

    private func copyPNG(from sourceURL: URL, to destinationURL: URL) throws {
        try fileManager.createDirectory(at: destinationURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        if fileManager.fileExists(atPath: destinationURL.path) {
            try fileManager.removeItem(at: destinationURL)
        }
        try fileManager.copyItem(at: sourceURL, to: destinationURL)
    }

    private func resetPetPanelForNewImage() {
        stopMotion()
        hideSpeechBubble()
        petPanel?.orderOut(nil)
        petPanel = nil
        imageView = nil
    }

    private func resetSpeechBubbleForNewImage() {
        speechBubblePanel?.orderOut(nil)
        speechBubblePanel = nil
        speechBubbleView = nil
        speechBubbleUntil = nil
    }

    private func runPNGOpenPanel(title: String) -> URL? {
        NSApp.activate(ignoringOtherApps: true)

        let panel = NSOpenPanel()
        panel.title = title
        panel.allowedContentTypes = [.png]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.resolvesAliases = true

        return panel.runModal() == .OK ? panel.url : nil
    }

    private func targetFrame(for image: NSImage) -> NSRect {
        let screenFrame = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1440, height: 900)
        let imageSize = image.size
        let aspectRatio = imageSize.width / max(imageSize.height, 1.0)
        let height = max(1.0, maxPetHeight * CGFloat(scalePercent / 100.0))
        let width = max(1.0, height * aspectRatio)
        let x = screenFrame.maxX - width - petMargin
        let y = screenFrame.minY + petMargin
        return NSRect(x: x, y: y, width: width, height: height)
    }

    private func resizePet() {
        guard let panel = petPanel, let image = imageView?.image else {
            return
        }

        restingFrame = targetFrame(for: image)
        petPosition = clampedPosition(restingFrame.origin, in: visibleFrame(), petSize: restingFrame.size)
        restingFrame.origin = petPosition
        panel.setFrame(restingFrame, display: true)
        imageView?.frame = NSRect(origin: .zero, size: restingFrame.size)
        startMotion()
    }

    private func showPet(animateFromBottom: Bool) {
        guard scalePercent > 0, let panel = makePetPanelIfNeeded(), let image = imageView?.image else {
            return
        }

        restingFrame = targetFrame(for: image)
        petPosition = restingFrame.origin
        walkTarget = petPosition
        imageView?.frame = NSRect(origin: .zero, size: restingFrame.size)

        if animateFromBottom {
            var startFrame = restingFrame
            startFrame.origin.y = (NSScreen.main?.visibleFrame.minY ?? 0) - restingFrame.height - 12
            panel.setFrame(startFrame, display: true)
            panel.orderFrontRegardless()

            NSAnimationContext.runAnimationGroup { context in
                context.duration = 0.65
                context.timingFunction = CAMediaTimingFunction(name: .easeOut)
                panel.animator().setFrame(restingFrame, display: true)
            } completionHandler: { [weak self] in
                self?.petPosition = self?.restingFrame.origin ?? .zero
                self?.chooseNextWalkTarget(force: true)
                self?.startMotion()
            }
        } else {
            panel.setFrame(restingFrame, display: true)
            panel.orderFrontRegardless()
            chooseNextWalkTarget(force: true)
            startMotion()
        }
    }

    private func startMotion() {
        stopMotion()
        guard petPanel?.isVisible == true, scalePercent > 0 else {
            return
        }

        motionStart = Date()
        lastMotionTick = motionStart
        let timer = Timer(timeInterval: 1.0 / 60.0, repeats: true) { [weak self] _ in
            self?.tickMotion()
        }
        motionTimer = timer
        RunLoop.main.add(timer, forMode: .common)
    }

    private func tickMotion() {
        guard let panel = petPanel, panel.isVisible else {
            return
        }

        let now = Date()
        let deltaTime = min(1.0 / 20.0, max(0.0, now.timeIntervalSince(lastMotionTick)))
        lastMotionTick = now

        var bobOffset: CGFloat = 0

        switch motionMode {
        case .wander:
            updateWander(now: now, deltaTime: deltaTime)
            bobOffset = currentWalkBob(now: now)
        case .bounce:
            bobOffset = sin(now.timeIntervalSince(motionStart) * 5.2) * 9.0
        case .rest:
            bobOffset = sin(now.timeIntervalSince(motionStart) * 2.1) * 3.0
        }

        bobOffset += currentJumpOffset(now: now)

        restingFrame.origin = petPosition
        var frame = restingFrame
        frame.origin.y += bobOffset
        panel.setFrame(frame, display: true)
        updateSpeechBubble(now: now, petFrame: frame)
    }

    private func updateWander(now: Date, deltaTime: TimeInterval) {
        guard restingFrame.size.width > 0, restingFrame.size.height > 0 else {
            return
        }

        if let restUntil, now < restUntil {
            return
        }

        restUntil = nil

        if now >= nextDecisionDate || distance(from: petPosition, to: walkTarget) < 14 {
            chooseNextWalkTarget(force: false)
        }

        let dx = walkTarget.x - petPosition.x
        let dy = walkTarget.y - petPosition.y
        let remainingDistance = max(1.0, hypot(dx, dy))
        let speedMultiplier: CGFloat = (dashUntil.map { now < $0 } ?? false) ? 2.35 : 1.0
        let step = min(remainingDistance, walkSpeed * speedMultiplier * CGFloat(deltaTime))

        petPosition.x += dx / remainingDistance * step
        petPosition.y += dy / remainingDistance * step
        petPosition = clampedPosition(petPosition, in: visibleFrame(), petSize: restingFrame.size)

        if abs(dx) > 0.5 {
            imageView?.facesRight = dx > 0
        }

        walkPhase += deltaTime * TimeInterval(7.0 * speedMultiplier)
    }

    private func chooseNextWalkTarget(force: Bool) {
        let now = Date()

        if !force {
            let roll = Double.random(in: 0...1)
            if roll < 0.14 {
                let until = now.addingTimeInterval(Double.random(in: 1.8...3.4))
                restUntil = until
                nextDecisionDate = until
                showSpeechBubble(until: until)
                return
            } else if roll < 0.24 {
                restUntil = now.addingTimeInterval(Double.random(in: 0.8...1.9))
                nextDecisionDate = restUntil ?? now
                return
            } else if roll < 0.32 {
                jumpStart = now
            } else if roll < 0.45 {
                dashUntil = now.addingTimeInterval(Double.random(in: 0.45...0.85))
            }
        }

        let screenFrame = visibleFrame()
        let maxX = max(screenFrame.minX + petMargin, screenFrame.maxX - restingFrame.width - petMargin)
        let maxY = max(screenFrame.minY + petMargin, screenFrame.maxY - restingFrame.height - petMargin)
        let minX = screenFrame.minX + petMargin
        let minY = screenFrame.minY + petMargin
        let targetY = Double.random(in: 0...1) < 0.68
            ? CGFloat.random(in: minY...max(minY, minY + (maxY - minY) * 0.42))
            : CGFloat.random(in: minY...maxY)

        walkTarget = NSPoint(
            x: CGFloat.random(in: minX...maxX),
            y: targetY
        )
        walkSpeed = CGFloat.random(in: 72...138)
        nextDecisionDate = now.addingTimeInterval(Double.random(in: 2.0...5.0))
    }

    private func currentWalkBob(now: Date) -> CGFloat {
        if let restUntil, now < restUntil {
            return sin(now.timeIntervalSince(motionStart) * 2.1) * 3.0
        }

        let speedMultiplier: CGFloat = (dashUntil.map { now < $0 } ?? false) ? 1.35 : 1.0
        return abs(sin(walkPhase)) * 7.5 * speedMultiplier
    }

    private func currentJumpOffset(now: Date) -> CGFloat {
        guard let jumpStart else {
            return 0
        }

        let duration = 0.7
        let elapsed = now.timeIntervalSince(jumpStart)
        if elapsed >= duration {
            self.jumpStart = nil
            return 0
        }

        return sin((elapsed / duration) * .pi) * 46.0
    }

    private func distance(from point: NSPoint, to otherPoint: NSPoint) -> CGFloat {
        hypot(point.x - otherPoint.x, point.y - otherPoint.y)
    }

    private func visibleFrame() -> NSRect {
        NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1440, height: 900)
    }

    private func clampedPosition(_ position: NSPoint, in screenFrame: NSRect, petSize: NSSize) -> NSPoint {
        let minX = screenFrame.minX + petMargin
        let minY = screenFrame.minY + petMargin
        let maxX = max(minX, screenFrame.maxX - petSize.width - petMargin)
        let maxY = max(minY, screenFrame.maxY - petSize.height - petMargin)

        return NSPoint(
            x: min(max(position.x, minX), maxX),
            y: min(max(position.y, minY), maxY)
        )
    }

    private func showSpeechBubble(until: Date) {
        speechBubbleUntil = until
        speechBubbleView?.message = speechBubbleText

        let panel = makeSpeechBubblePanelIfNeeded()
        positionSpeechBubble(above: petPanel?.frame ?? restingFrame)
        panel.orderFrontRegardless()
    }

    private func updateSpeechBubble(now: Date, petFrame: NSRect) {
        guard let until = speechBubbleUntil else {
            return
        }

        if now >= until || petPanel?.isVisible != true {
            hideSpeechBubble()
            return
        }

        let panel = makeSpeechBubblePanelIfNeeded()
        positionSpeechBubble(above: petFrame)
        if !panel.isVisible {
            panel.orderFrontRegardless()
        }
    }

    private func positionSpeechBubble(above petFrame: NSRect) {
        guard let panel = speechBubblePanel else {
            return
        }

        let screenFrame = visibleFrame()
        let size = speechBubbleSize
        let rawX = petFrame.midX - size.width / 2
        let rawY = petFrame.maxY + 4
        let x = min(max(rawX, screenFrame.minX + 8), screenFrame.maxX - size.width - 8)
        let y = min(max(rawY, screenFrame.minY + 8), screenFrame.maxY - size.height - 8)

        panel.setFrame(NSRect(x: x, y: y, width: size.width, height: size.height), display: true)
    }

    private func hideSpeechBubble() {
        speechBubbleUntil = nil
        speechBubblePanel?.orderOut(nil)
    }

    private func stopMotion() {
        motionTimer?.invalidate()
        motionTimer = nil
    }

    private func hidePetOnly() {
        stopMotion()
        hideSpeechBubble()
        petPanel?.orderOut(nil)
    }

    @objc private func showPetFromMenu() {
        if scalePercent <= 0 {
            setScalePercent(defaultScalePercent)
        } else {
            showPet(animateFromBottom: true)
        }
    }

    @objc private func hidePetFromMenu() {
        setScalePercent(0)
    }

    @objc private func sliderChanged(_ sender: NSSlider) {
        setScalePercent(sender.doubleValue)
    }

    @objc private func choosePetImage() {
        guard let sourceURL = runPNGOpenPanel(title: "펫 이미지 선택") else {
            return
        }

        do {
            try copyPNG(from: sourceURL, to: userPetImageURL)
            resetPetPanelForNewImage()
            if scalePercent <= 0 {
                setScalePercent(defaultScalePercent)
            } else {
                showPet(animateFromBottom: true)
            }
        } catch {
            NSLog("펫 이미지를 저장할 수 없습니다: \(error.localizedDescription)")
        }
    }

    @objc private func chooseSpeechImage() {
        guard let sourceURL = runPNGOpenPanel(title: "말풍선 이미지 선택") else {
            return
        }

        do {
            try copyPNG(from: sourceURL, to: userSpeechBubbleImageURL)
            resetSpeechBubbleForNewImage()
        } catch {
            NSLog("말풍선 이미지를 저장할 수 없습니다: \(error.localizedDescription)")
        }
    }

    @objc private func motionModeChanged(_ sender: NSMenuItem) {
        guard let newMode = MotionMode(rawValue: sender.tag) else {
            return
        }

        motionMode = newMode
        UserDefaults.standard.set(newMode.rawValue, forKey: savedMotionModeKey)
        updateMotionMenuItems()
        restUntil = nil
        hideSpeechBubble()
        jumpStart = nil
        dashUntil = nil

        if newMode == .wander {
            chooseNextWalkTarget(force: true)
        }

        if petPanel?.isVisible == true {
            startMotion()
        }
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
