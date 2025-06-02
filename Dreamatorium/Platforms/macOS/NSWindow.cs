using SharpMetal;
using SharpMetal.Foundation;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

[Flags]
public enum NSStyleMask : ulong
{
    Borderless = 0,
    Titled = 1 << 0,
    Closable = 1 << 1,
    Miniaturizable = 1 << 2,
    Resizable = 1 << 3,
    FullScreen = 1 << 14,
    FullSizeContentView = 1 << 15,
    UtilityWindow = 1 << 4,
    DocModalWindow = 1 << 6,
    NonactivatingPanel = 1 << 7,
    HUDWindow = 1 << 13
}

public readonly struct NSWindow
{
    public readonly nint NativePtr;

    public NSWindow(NSRect rect, ulong styleMask)
    {
        NativePtr = new ObjectiveCClass("NSWindow").Alloc();
        ObjectiveC.objc_msgSend(NativePtr, "initWithContentRect:styleMask:backing:defer:", rect, styleMask, 2, false);
    }

    public NSString Title
    {
        get => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "title"));
        set => ObjectiveC.objc_msgSend(NativePtr, "setTitle:", value);
    }

    public float BackingScaleFactor => (float)ObjectiveCRuntime.double_objc_msgSend(NativePtr, new Selector("backingScaleFactor"));

    public void SetContentView(nint ptr)
    {
        ObjectiveC.objc_msgSend(NativePtr, "setContentView:", ptr);
    }

    public void MakeKeyAndOrderFront()
    {
        ObjectiveC.objc_msgSend(NativePtr, "makeKeyAndOrderFront:", nint.Zero);
    }

    public bool MakeFirstResponder(nint ptr)
    {
        return ObjectiveC.bool_objc_msgSend(NativePtr, "makeFirstResponder:", ptr);
    }
}
