using System.Runtime.InteropServices;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

public class MTKViewDelegate
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnMTKViewDrawableSizeWillChangeDelegate(nint id, nint cmd, nint view, NSRect size);

    private ObjCMessageNintNintNintDelegate _onDrawInMTKView;
    private OnMTKViewDrawableSizeWillChangeDelegate _onMtkViewDrawableSizeWillChange;

    public Action<MTKView> OnDrawInMTKView;
    public Action<MTKView, NSRect> OnMTKViewDrawableSizeWillChange;

    public nint NativePtr;

    public static implicit operator nint(MTKViewDelegate mtkDelegate) => mtkDelegate.NativePtr;

    public MTKViewDelegate(Engine engine)
    {
        OnDrawInMTKView += engine.Update;
        OnMTKViewDrawableSizeWillChange += engine.Resize;

        _onDrawInMTKView = (_, _, view) => OnDrawInMTKView(new MTKView(view));
        _onMtkViewDrawableSizeWillChange = (_, _, view, rect) => OnMTKViewDrawableSizeWillChange(new MTKView(view), rect);
        InteropKeepAliveRegistry.Add(_onDrawInMTKView);
        InteropKeepAliveRegistry.Add(_onMtkViewDrawableSizeWillChange);

        var builder = new ObjectiveCClassBuilder("MyMTKViewDelegate")
            .SetSuperClass("NSObject")
            .AddMethod("drawInMTKView:", Marshal.GetFunctionPointerForDelegate(_onDrawInMTKView), "v@:@")
            .AddMethod("mtkView:drawableSizeWillChange:", Marshal.GetFunctionPointerForDelegate(_onMtkViewDrawableSizeWillChange), "v@:#{CGRect={CGPoint=dd}{CGPoint=dd}}");

        var mtkDelegateClass = builder.Build();
        NativePtr = new ObjectiveCClass(mtkDelegateClass).AllocInit();
    }
}
