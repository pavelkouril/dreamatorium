using SharpMetal;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

public readonly struct NSEvent(nint ptr)
{
    public readonly nint NativePtr = ptr;

    private static readonly Selector s_selKeyCode = (Selector)"keyCode";
    private static readonly Selector s_selIsARepeat = (Selector)"isARepeat";
    private static readonly Selector s_selScrollingDeltaX = (Selector)"scrollingDeltaX";
    private static readonly Selector s_selScrollingDeltaY = (Selector)"scrollingDeltaY";

    public ushort KeyCode => ObjectiveC.ushort_objc_msgSend(this.NativePtr, s_selKeyCode);
    public bool IsRepeat => ObjectiveC.bool_objc_msgSend(this.NativePtr, s_selIsARepeat);
    public float ScrollingDeltaX => (float)ObjectiveC.double_objc_msgSend(this.NativePtr, s_selScrollingDeltaX);
    public float ScrollingDeltaY => (float)ObjectiveC.double_objc_msgSend(this.NativePtr, s_selScrollingDeltaY);

    public static implicit operator nint(NSEvent obj) => obj.NativePtr;
}
