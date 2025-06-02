using SharpMetal.ObjectiveCCore;

namespace Dreamatorium.Platforms.macOS;

public readonly struct NSApplication(nint ptr)
{
    public readonly nint NativePtr = ptr;

    public NSApplication() : this(ObjectiveC.IntPtr_objc_msgSend(new ObjectiveCClass("NSApplication"), "sharedApplication")) { }

    public void Run()
    {
        ObjectiveC.objc_msgSend(NativePtr, "run");
    }

    public void Stop()
    {
        ObjectiveC.objc_msgSend(NativePtr, "stop:", nint.Zero);
    }

    public void ActivateIgnoringOtherApps(bool flag)
    {
        ObjectiveC.objc_msgSend(NativePtr, "activateIgnoringOtherApps:", flag);
    }

    public bool SetActivationPolicy(NSApplicationActivationPolicy activationPolicy)
    {
        return ObjectiveC.bool_objc_msgSend(NativePtr, "setActivationPolicy:", (long)activationPolicy);
    }

    public void SetDelegate(NSApplicationDelegate appDelegate)
    {
        ObjectiveC.objc_msgSend(NativePtr, "setDelegate:", appDelegate.NativePtr);
    }
}

public enum NSApplicationActivationPolicy : long
{
    Regular = 0,
    Accessory = 1,
    Prohibited = 2
}
