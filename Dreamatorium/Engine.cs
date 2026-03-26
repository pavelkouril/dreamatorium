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
    private readonly List<Mesh> _scene;

    private readonly Camera _camera;

    private readonly RenderingPipeline _pipeline;
    private readonly ImGuiRenderPass _imGuiRenderPass;
    private readonly AppUi _appUi;
    private readonly FrameCaptureController _frameCaptureController;

    private int _frameCount;

    private float _lastFrameTime;

    public InputManager InputManager { get; private set; }

    public Engine(MTLDevice device, InputManager inputManager, ulong initialWidth, ulong initialHeight)
    {
        var assetLoader = new AssetLoader(device);
        var loader = new SponzaLoader();
        _scene = loader.LoadFromFile(assetLoader, "Data/sponza/sponza.obj", device);

        var skyboxTexture = assetLoader.LoadTexture("Data/skybox_to_equirect_2.png", TextureType.None);

        _camera = new Camera(initialWidth / (float)initialHeight);

        _pipeline = new RenderingPipeline(device, _scene, skyboxTexture, _camera, initialWidth, initialHeight);
        _appUi = new AppUi();
        _imGuiRenderPass = new ImGuiRenderPass(device, _appUi);
        _frameCaptureController = new FrameCaptureController(device);

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

        var currentDrawable = view.CurrentDrawable;
        if (currentDrawable.NativePtr == nint.Zero)
        {
            return;
        }

        InputCaptureState captureState = _imGuiRenderPass.BeginFrame(frameInput, view, currentDrawable.Texture);
        FrameInput cameraInput = frameInput.ConsumeCapturedInputs(captureState);
        _camera.ProcessInput(cameraInput);

        bool hasRequestedFrameCapture = _appUi.TryConsumeFrameCaptureRequest();
        _frameCaptureController.BeginCaptureIfRequested(hasRequestedFrameCapture, frameInput.Frame);

        _pipeline.Render(frameInput, currentDrawable.Texture.Width, currentDrawable.Texture.Height);

        var frameCommandBuffer = _pipeline.CreateFrameCommandBuffer("Frame Command Buffer");
        var blitEncoder = frameCommandBuffer.BlitCommandEncoder(new MTLBlitPassDescriptor());
        blitEncoder.Label = StringHelper.NSString("Blit/Encoder");
        blitEncoder.CopyFromTexture(_pipeline.FinalTexture, currentDrawable.Texture);
        blitEncoder.EndEncoding();

        _imGuiRenderPass.Render(frameInput, frameCommandBuffer, currentDrawable.Texture);
        frameCommandBuffer.PresentDrawable(currentDrawable);
        frameCommandBuffer.Commit();

        _frameCaptureController.EndCaptureAndReveal();
    }

    public void Resize(MTKView view, NSRect size)
    {
    }
}
