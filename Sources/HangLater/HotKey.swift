import AppKit
import Carbon

/// Uses the Carbon shortcut API without requiring Accessibility access.
final class HotKey {
    private var reference: EventHotKeyRef?
    private var handler: EventHandlerRef?
    private let callback: () -> Void
    init?(callback: @escaping () -> Void) {
        self.callback = callback
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let context = Unmanaged.passUnretained(self).toOpaque()
        let installed = InstallEventHandler(GetApplicationEventTarget(), { _, _, context in
            guard let context else { return noErr }
            let key = Unmanaged<HotKey>.fromOpaque(context).takeUnretainedValue()
            DispatchQueue.main.async { key.callback() }
            return noErr
        }, 1, &spec, context, &handler)
        guard installed == noErr else { return nil }
        let id = EventHotKeyID(signature: OSType(0x48414E47), id: 1)
        guard RegisterEventHotKey(UInt32(kVK_ANSI_L), UInt32(controlKey | optionKey), id,
                                  GetApplicationEventTarget(), 0, &reference) == noErr else { return nil }
    }
    deinit {
        if let reference { UnregisterEventHotKey(reference) }
        if let handler { RemoveEventHandler(handler) }
    }
}
