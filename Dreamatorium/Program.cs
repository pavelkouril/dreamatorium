using Dreamatorium.Input;
using Dreamatorium.Platforms;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;

namespace Dreamatorium;

public class Program
{
    private const int kPositionX = 100;
    private const int kPositionY = 100;
    private const int kWidth = 1024;
    private const int kHeight = 768;
    private const string kWindowTitle = "Dreamatorium";

    public static void Main()
    {
        ObjectiveC.LinkMetal();
        ObjectiveC.LinkCoreGraphics();
        ObjectiveC.LinkAppKit();
        ObjectiveC.LinkMetalKit();

        var nsApplication = new NSApplication();
        var appDelegate = new NSApplicationDelegate(nsApplication);
        nsApplication.SetDelegate(appDelegate);

        appDelegate.OnApplicationDidFinishLaunching += OnApplicationDidFinishLaunching;
        appDelegate.OnApplicationWillFinishLaunching += OnApplicationWillFinishLaunching;

        nsApplication.Run();
    }

    private static void OnApplicationWillFinishLaunching(NSNotification notification)
    {
        var app = new NSApplication(notification.Object);
        app.SetActivationPolicy(NSApplicationActivationPolicy.Regular);
    }

    private static void OnApplicationDidFinishLaunching(NSNotification notification)
    {
        var rect = new NSRect(kPositionX, kPositionY, kWidth, kHeight);
        var window = new NSWindow(rect, (ulong)(NSStyleMask.Titled | NSStyleMask.Closable | NSStyleMask.Miniaturizable));

        var device = MTLDevice.CreateSystemDefaultDevice();

        var inputManager = new InputManager();

        var mtkView = new MTKView(inputManager, rect, device)
        {
            ColorPixelFormat = MTLPixelFormat.RGBA8UnormsRGB,
            ClearColor = new MTLClearColor { red = 0.0, green = 0.0, blue = 0.0, alpha = 1.0 },
            FrameBufferOnly = false,
        };
        InteropKeepAliveRegistry.Add(mtkView);

        float backingScaleFactor = window.BackingScaleFactor;
        var engine = new Engine(device, inputManager, (ulong)(kWidth * backingScaleFactor), (ulong)(kHeight * backingScaleFactor));

        mtkView.Delegate = new MTKViewDelegate(engine);

        window.SetContentView(mtkView);
        window.Title = StringHelper.NSString(kWindowTitle);
        window.MakeKeyAndOrderFront();
        window.MakeFirstResponder(mtkView);

        var app = new NSApplication(notification.Object);
        app.ActivateIgnoringOtherApps(true);
    }
}
