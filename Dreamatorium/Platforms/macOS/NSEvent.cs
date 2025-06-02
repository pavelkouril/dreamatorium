using SharpMetal;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

public readonly struct NSEvent(nint ptr)
{
    public readonly nint NativePtr = ptr;

    private static readonly Selector s_selKeyCode = (Selector)"keyCode";
    private static readonly Selector s_selIsARepeat = (Selector)"isARepeat";

    public ushort KeyCode => ObjectiveCRuntime.ushort_objc_msgSend(this.NativePtr, s_selKeyCode);
    public bool IsRepeat => ObjectiveCRuntime.bool_objc_msgSend(this.NativePtr, s_selIsARepeat);

    public static implicit operator nint(NSEvent obj) => obj.NativePtr;
}
