using System.Diagnostics;
using System.Runtime.Versioning;
using Dreamatorium.Input;
using Dreamatorium.Rendering.Resources;
using Dreamatorium.Scene;
using Dreamatorium.Rendering;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;

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
        var loader = new SponzaLoader();
        _scene = loader.LoadFromFile("Data/sponza/sponza.obj", device);

        _camera = new Camera(initialWidth / (float)initialHeight);

        _pipeline = new RenderingPipeline(device, _scene, _camera, initialWidth, initialHeight);

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
