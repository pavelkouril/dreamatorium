using System.Runtime.InteropServices;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

internal static class ObjectiveCRuntimePointExtensions
{
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern NSPoint NSPoint_objc_msgSend(nint receiver, Selector selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern NSPoint NSPoint_objc_msgSend(nint receiver, Selector selector, NSPoint point, nint fromView);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern NSPoint NSPoint_objc_msgSend(nint receiver, Selector selector, NSPoint point);

    public static NSPoint objc_msgSend(nint receiver, Selector selector)
    {
        return NSPoint_objc_msgSend(receiver, selector);
    }

    public static NSPoint objc_msgSend(nint receiver, Selector selector, NSPoint point, nint fromView)
    {
        return NSPoint_objc_msgSend(receiver, selector, point, fromView);
    }

    public static NSPoint objc_msgSend(nint receiver, Selector selector, NSPoint point)
    {
        return NSPoint_objc_msgSend(receiver, selector, point);
    }
}
