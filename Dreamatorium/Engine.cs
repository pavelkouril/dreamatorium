using System.Diagnostics;
using Assimp;
using Dreamatorium.Assets;
using Dreamatorium.Input;
using Dreamatorium.Rendering;
using Dreamatorium.Platforms.macOS;
using Dreamatorium.UI;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;
using Camera = Dreamatorium.Scene.Camera;
using Mesh = Dreamatorium.Rendering.Resources.Mesh;

namespace Dreamatorium;

public class Engine
{
    private readonly Stopwatch _watch = new Stopwatch();
    private readonly MTLDevice _device;

    private readonly List<Mesh> _scene;

    private readonly Camera _camera;

    private readonly RenderingPipeline _pipeline;
    private readonly ImGuiRenderPass _imGuiRenderPass;

    private int _frameCount;

    private float _lastFrameTime;

    public InputManager InputManager { get; private set; }

    public Engine(MTLDevice device, InputManager inputManager, ulong initialWidth, ulong initialHeight)
    {
        _device = device;

        var assetLoader = new AssetLoader(device);
        var loader = new SponzaLoader();
        _scene = loader.LoadFromFile(assetLoader, "Data/sponza/sponza.obj", device);

        var skyboxTexture = assetLoader.LoadTexture("Data/skybox_to_equirect_2.png", TextureType.None);

        _camera = new Camera(initialWidth / (float)initialHeight);

        _pipeline = new RenderingPipeline(device, _scene, skyboxTexture, _camera, initialWidth, initialHeight);
        _imGuiRenderPass = new ImGuiRenderPass(device, new AppUi());

        InputManager = inputManager;

        _watch.Start();
    }

    public void Update(MTKView view)
    {
        view.UpdateMousePosition(InputManager);

        float totalElapsed = _watch.ElapsedMilliseconds;
        var deltaTimeInMs = totalElapsed - _lastFrameTime;
        _lastFrameTime = totalElapsed;
        var frameInput = new FrameInput(_frameCount++, totalElapsed, deltaTimeInMs / 1000.0f, InputManager.CaptureSnapshotAndSwap());

        _camera.ProcessInput(frameInput);

        var currentDrawable = view.CurrentDrawable;
        if (currentDrawable.NativePtr == nint.Zero)
        {
            return;
        }

        bool hasRequestedFrameCapture = frameInput.HasKeyEvent(KeyCode.P, KeyEventType.KeyDown) && !frameInput.HasKeyEvent(KeyCode.P, KeyEventType.IsRepeat);
        MTLCaptureManager captureManager = default;
        if (hasRequestedFrameCapture)
        {
            captureManager = MTLCaptureManager.SharedCaptureManager;
            var desc = new MTLCaptureDescriptor
            {
                CaptureObject = new SharpMetal.Foundation.NSObject(_device),
                Destination = MTLCaptureDestination.GPUTraceDocument
            };
            string captureFileName = $"capture_{frameInput.Frame}.gputrace";
            Console.WriteLine($"Capturing trace to {captureFileName}");
            desc.OutputURL = SharpMetal.Foundation.NSURL.FileURLWithPath(StringHelper.NSString(captureFileName));
            SharpMetal.Foundation.NSError error = default;
            captureManager.StartCapture(desc, ref error);
            if (error.Code != 0)
            {
                Console.WriteLine(StringHelper.String(error.LocalizedDescription));
            }
        }

        _pipeline.Render(frameInput, currentDrawable.Texture.Width, currentDrawable.Texture.Height);

        var frameCommandBuffer = _pipeline.CreateFrameCommandBuffer("Frame Command Buffer");
        var blitEncoder = frameCommandBuffer.BlitCommandEncoder(new MTLBlitPassDescriptor());
        blitEncoder.Label = StringHelper.NSString("Blit/Encoder");
        blitEncoder.CopyFromTexture(_pipeline.FinalTexture, currentDrawable.Texture);
        blitEncoder.EndEncoding();

        _imGuiRenderPass.Execute(frameInput, view, frameCommandBuffer, currentDrawable.Texture);
        frameCommandBuffer.PresentDrawable(currentDrawable);
        frameCommandBuffer.Commit();

        if (captureManager.NativePtr != nint.Zero && captureManager.IsCapturing)
        {
            captureManager.StopCapture();
        }
    }

    public void Resize(MTKView view, NSRect size)
    {
    }
}
