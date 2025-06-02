using System.Diagnostics;
using Assimp;
using Dreamatorium.Assets;
using Dreamatorium.Input;
using Dreamatorium.Rendering;
using Dreamatorium.Platforms.macOS;
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

        InputManager = inputManager;

        _watch.Start();
    }

    public void Update(MTKView view)
    {
        float totalElapsed = _watch.ElapsedMilliseconds;
        var deltaTimeInMs = totalElapsed - _lastFrameTime;
        _lastFrameTime = totalElapsed;
        var frameInput = new FrameInput(_frameCount++, totalElapsed, deltaTimeInMs / 1000.0f, InputManager.ReturnCurrentBufferAndSwap());

        _camera.ProcessInput(frameInput);

        _pipeline.Render(frameInput, view);
    }

    public void Resize(MTKView view, NSRect size)
    {
    }
}
