using Dreamatorium.Platforms;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public sealed class FrameCaptureController
{
    private readonly MTLDevice _device;

    private MTLCaptureManager _captureManager;
    private string? _captureFilePath;

    public FrameCaptureController(MTLDevice device)
    {
        _device = device;
    }

    public void BeginCaptureIfRequested(bool captureRequested, int frame)
    {
        if (!captureRequested || IsCapturing)
        {
            return;
        }

        _captureManager = MTLCaptureManager.SharedCaptureManager;
        var descriptor = new MTLCaptureDescriptor
        {
            CaptureObject = new SharpMetal.Foundation.NSObject(_device),
            Destination = MTLCaptureDestination.GPUTraceDocument
        };

        _captureFilePath = Path.GetFullPath($"capture_{frame}.gputrace");
        Console.WriteLine($"Capturing trace to {_captureFilePath}");

        descriptor.OutputURL = SharpMetal.Foundation.NSURL.FileURLWithPath(StringHelper.NSString(_captureFilePath));
        SharpMetal.Foundation.NSError error = default;
        _captureManager.StartCapture(descriptor, ref error);
        if (error.Code != 0)
        {
            Console.WriteLine(StringHelper.String(error.LocalizedDescription));
            _captureFilePath = null;
        }
    }

    public void EndCaptureAndReveal()
    {
        if (!IsCapturing)
        {
            return;
        }

        _captureManager.StopCapture();
        PlatformShell.RevealInFileManager(_captureFilePath);
        _captureFilePath = null;
        _captureManager = default;
    }

    private bool IsCapturing => _captureManager.NativePtr != nint.Zero && _captureManager.IsCapturing;
}
