using System.Runtime.InteropServices;
using Dreamatorium.Input;
using SharpMetal;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;
using SharpMetal.QuartzCore;

namespace Dreamatorium.Platforms.macOS;

public readonly struct MTKView
{
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

        var builder = new ObjectiveCClassBuilder("MyMTKView")
            .SetSuperClass("MTKView")
            .AddMethod("keyDown:", Marshal.GetFunctionPointerForDelegate(_onKeyDown), "v@:@")
            .AddMethod("keyUp:", Marshal.GetFunctionPointerForDelegate(_onKeyUp), "v@:@");

        var myMtkViewClass = builder.Build();

        var ptr = new ObjectiveCClass(myMtkViewClass).Alloc();
        NativePtr = ObjectiveC.IntPtr_objc_msgSend(ptr, "initWithFrame:device:", frameRect, device);
    }

    public MTLPixelFormat ColorPixelFormat
    {
        set => ObjectiveC.objc_msgSend(NativePtr, "setColorPixelFormat:atIndex:", (ulong)value, 0);
    }

    public MTLClearColor ClearColor
    {
        set => ObjectiveCRuntime.objc_msgSend(NativePtr, new Selector("setClearColor:"), value);
    }

    public bool FrameBufferOnly
    {
        set => ObjectiveCRuntime.objc_msgSend(NativePtr, new Selector("setFramebufferOnly:"), (bool)value);
    }

    public MTKViewDelegate Delegate
    {
        set => ObjectiveC.objc_msgSend(NativePtr, "setDelegate:", value);
    }

    public CAMetalDrawable CurrentDrawable => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentDrawable"));

    public MTLRenderPassDescriptor CurrentRenderPassDescriptor => new(ObjectiveC.IntPtr_objc_msgSend(NativePtr, "currentRenderPassDescriptor"));

    public static implicit operator nint(MTKView mtkView) => mtkView.NativePtr;
}
