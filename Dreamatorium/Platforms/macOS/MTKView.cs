using System.Runtime.InteropServices;
using Dreamatorium.Input;
using SharpMetal;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;
using SharpMetal.QuartzCore;

namespace Dreamatorium.Platforms.macOS;

public readonly struct MTKView
{
    private static readonly nint s_nsEventClass = new ObjectiveCClass("NSEvent");
    private static readonly Selector s_selMouseLocation = (Selector)"mouseLocation";
    private static readonly Selector s_selConvertPointFromScreen = (Selector)"convertPointFromScreen:";
    private static readonly Selector s_selConvertPointFromView = (Selector)"convertPoint:fromView:";

    public readonly nint NativePtr;

    public MTKView(nint ptr)
    {
        NativePtr = ptr;
    }

    public MTKView(InputManager inputManager, NSRect frameRect, MTLDevice device)
    {
        var _onKeyDown = (ObjCMessageNintNintNintDelegate)((_, _, @event) =>
        {
            var e = new NSEvent(@event);
            inputManager.RecordKeyEvent(e.KeyCode, KeyEventType.KeyDown, e.IsRepeat);
        });
        InteropKeepAliveRegistry.Add(_onKeyDown);

        var _onKeyUp = (ObjCMessageNintNintNintDelegate)((_, _, @event) =>
        {
            var e = new NSEvent(@event);
            inputManager.RecordKeyEvent(e.KeyCode, KeyEventType.KeyUp, e.IsRepeat);
        });
        InteropKeepAliveRegistry.Add(_onKeyUp);

        var _onFlagsChanged = (ObjCMessageNintNintNintDelegate)((_, _, @event) =>
        {
            var e = new NSEvent(@event);
            inputManager.RecordModifierFlagsChanged(e.KeyCode);
        });
        InteropKeepAliveRegistry.Add(_onFlagsChanged);

        var _onMouseDown = (ObjCMessageNintNintNintDelegate)((_, _, _) =>
        {
            inputManager.RecordMouseButton(0, true);
        });
        InteropKeepAliveRegistry.Add(_onMouseDown);

        var _onMouseUp = (ObjCMessageNintNintNintDelegate)((_, _, _) =>
        {
            inputManager.RecordMouseButton(0, false);
        });
        InteropKeepAliveRegistry.Add(_onMouseUp);

        var _onRightMouseDown = (ObjCMessageNintNintNintDelegate)((_, _, _) =>
        {
            inputManager.RecordMouseButton(1, true);
        });
        InteropKeepAliveRegistry.Add(_onRightMouseDown);

        var _onRightMouseUp = (ObjCMessageNintNintNintDelegate)((_, _, _) =>
        {
            inputManager.RecordMouseButton(1, false);
        });
        InteropKeepAliveRegistry.Add(_onRightMouseUp);

        var _onOtherMouseDown = (ObjCMessageNintNintNintDelegate)((_, _, _) =>
        {
            inputManager.RecordMouseButton(2, true);
        });
        InteropKeepAliveRegistry.Add(_onOtherMouseDown);

        var _onOtherMouseUp = (ObjCMessageNintNintNintDelegate)((_, _, _) =>
        {
            inputManager.RecordMouseButton(2, false);
        });
        InteropKeepAliveRegistry.Add(_onOtherMouseUp);

        var _onScrollWheel = (ObjCMessageNintNintNintDelegate)((_, _, @event) =>
        {
            var e = new NSEvent(@event);
            inputManager.RecordMouseWheel(e.ScrollingDeltaX, e.ScrollingDeltaY);
        });
        InteropKeepAliveRegistry.Add(_onScrollWheel);

        var builder = new ObjectiveCClassBuilder("MyMTKView")
            .SetSuperClass("MTKView")
            .AddMethod("keyDown:", Marshal.GetFunctionPointerForDelegate(_onKeyDown), "v@:@")
            .AddMethod("keyUp:", Marshal.GetFunctionPointerForDelegate(_onKeyUp), "v@:@")
            .AddMethod("flagsChanged:", Marshal.GetFunctionPointerForDelegate(_onFlagsChanged), "v@:@")
            .AddMethod("mouseDown:", Marshal.GetFunctionPointerForDelegate(_onMouseDown), "v@:@")
            .AddMethod("mouseUp:", Marshal.GetFunctionPointerForDelegate(_onMouseUp), "v@:@")
            .AddMethod("rightMouseDown:", Marshal.GetFunctionPointerForDelegate(_onRightMouseDown), "v@:@")
            .AddMethod("rightMouseUp:", Marshal.GetFunctionPointerForDelegate(_onRightMouseUp), "v@:@")
            .AddMethod("otherMouseDown:", Marshal.GetFunctionPointerForDelegate(_onOtherMouseDown), "v@:@")
            .AddMethod("otherMouseUp:", Marshal.GetFunctionPointerForDelegate(_onOtherMouseUp), "v@:@")
            .AddMethod("scrollWheel:", Marshal.GetFunctionPointerForDelegate(_onScrollWheel), "v@:@");

        var myMtkViewClass = builder.Build();

        var ptr = new ObjectiveCClass(myMtkViewClass).Alloc();
        NativePtr = ObjectiveC.IntPtr_objc_msgSend(ptr, "initWithFrame:device:", frameRect, device);
    }

    public void UpdateMousePosition(InputManager inputManager)
    {
        nint window = ObjectiveC.IntPtr_objc_msgSend(NativePtr, "window");
        if (window == nint.Zero)
        {
            return;
        }

        NSPoint screenPosition = ObjectiveCRuntimePointExtensions.objc_msgSend(s_nsEventClass, s_selMouseLocation);
        NSPoint windowPosition = ObjectiveCRuntimePointExtensions.objc_msgSend(window, s_selConvertPointFromScreen, screenPosition);
        NSPoint localPosition = ObjectiveCRuntimePointExtensions.objc_msgSend(NativePtr, s_selConvertPointFromView, windowPosition, nint.Zero);
        inputManager.RecordMouseMove((float)localPosition.X, (float)localPosition.Y);
    }

    public MTLPixelFormat ColorPixelFormat
    {
        set => ObjectiveC.objc_msgSend(NativePtr, "setColorPixelFormat:atIndex:", (ulong)value, 0);
    }

    public float BackingScaleFactor
    {
        get
        {
            nint window = ObjectiveC.IntPtr_objc_msgSend(NativePtr, "window");
            if (window == nint.Zero)
            {
                return 1.0f;
            }

            return (float)ObjectiveC.double_objc_msgSend(window, (Selector)"backingScaleFactor");
        }
    }

    public MTLClearColor ClearColor
    {
        set => ObjectiveC.objc_msgSend(NativePtr, new Selector("setClearColor:"), value);
    }

    public bool FrameBufferOnly
    {
        set => ObjectiveC.objc_msgSend(NativePtr, new Selector("setFramebufferOnly:"), (bool)value);
    }

    public MTKViewDelegate Delegate
    {
        set => ObjectiveC.objc_msgSend(NativePtr, "setDelegate:", value);
    }

    public CAMetalDrawable CurrentDrawable => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentDrawable"));

    public MTLRenderPassDescriptor CurrentRenderPassDescriptor => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentRenderPassDescriptor"));

    public static implicit operator nint(MTKView mtkView) => mtkView.NativePtr;
}
