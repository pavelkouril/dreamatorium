using System.Runtime.InteropServices;
using SharpMetal.Foundation;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

public class NSApplicationDelegate
{
    private ObjCMessageNintNintNintDelegate _onApplicationWillFinishLaunching;
    private ObjCMessageNintNintNintDelegate _onApplicationDidFinishLaunching;
    private ObjCMessageNintNintNintReturnBoolDelegate _onApplicationShouldTerminateAfterLastWindowClosed;

    public Action<NSNotification>? OnApplicationWillFinishLaunching { get; set; }
    public Action<NSNotification>? OnApplicationDidFinishLaunching { get; set; }

    public nint NativePtr { get; private set; }

    public NSApplicationDelegate(NSApplication application)
    {
        _onApplicationWillFinishLaunching = (_, _, notif) => OnApplicationWillFinishLaunching?.Invoke(new NSNotification(notif));
        var onApplicationWillFinishLaunchingPtr = Marshal.GetFunctionPointerForDelegate(_onApplicationWillFinishLaunching);

        _onApplicationDidFinishLaunching = (_, _, notif) => OnApplicationDidFinishLaunching?.Invoke(new NSNotification(notif));
        var onDidFinishLaunchingPtr = Marshal.GetFunctionPointerForDelegate(_onApplicationDidFinishLaunching);

        _onApplicationShouldTerminateAfterLastWindowClosed = (_, _, _) => true;
        var onApplicationShouldTerminateAfterLastWindowClosedPointer = Marshal.GetFunctionPointerForDelegate(_onApplicationShouldTerminateAfterLastWindowClosed);

        var builder = new ObjectiveCClassBuilder("AppDelegate")
            .SetSuperClass("NSObject")
            .AddMethod("applicationWillFinishLaunching:", onApplicationWillFinishLaunchingPtr, "v@:@")
            .AddMethod("applicationDidFinishLaunching:", onDidFinishLaunchingPtr, "v@:@")
            .AddMethod("applicationShouldTerminateAfterLastWindowClosed:", onApplicationShouldTerminateAfterLastWindowClosedPointer,"B@:@");

        var appDelegateClass = builder.Build();
        NativePtr = new ObjectiveCClass(appDelegateClass).AllocInit();
    }
}
