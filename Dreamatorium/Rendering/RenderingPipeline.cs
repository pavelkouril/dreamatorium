using Dreamatorium.Input;
using Dreamatorium.Rendering.Resources;
using Dreamatorium.Scene;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class RenderingPipeline
{
    public const MTLPixelFormat kGBufferAFormat = MTLPixelFormat.RGBA8UnormsRGB;
    public const MTLPixelFormat kGBufferBFormat = MTLPixelFormat.RGBA8Snorm;
    public const MTLPixelFormat kDepthFormat = MTLPixelFormat.R32Float;

    private MTLDevice _device;

    private readonly List<IPass> _renderPasses = [];

    /// <summary>
    /// GBuffer Texture containing BaseColor + Roughness
    /// </summary>
    public MTLTexture GBufferA { get; private set; }

    /// <summary>
    /// GBuffer Texture containing WorldSpace Normals + Metalness
    /// </summary>
    public MTLTexture GBufferB { get; private set; }

    /// <summary>
    /// View space Depth in R32 Format
    /// </summary>
    public MTLTexture GBufferDepth { get; private set; }

    /// <summary>
    /// Clip space Depth + Stencil Buffer
    /// </summary>
    public MTLTexture DepthStencil { get; private set; }

    private readonly GeometryPass _geometryPass;
    private readonly BlitPass _blitPass;

    private MTLCommandQueue _queue;

    public RenderingPipeline(MTLDevice device, List<Mesh> scene, Camera camera, ulong initialWidth, ulong initialHeight)
    {
        _device = device;

        _queue = device.NewCommandQueue();

        CreateGBuffer(initialWidth, initialHeight);

        _geometryPass = new GeometryPass(_device, _queue, this, scene, camera);
        _blitPass = new BlitPass(_queue);
    }

    public void Render(in FrameInput frameInput, MTKView view)
    {
        bool hasRequestedFrameCapture = frameInput.HasKeyEvent(KeyCode.P, KeyEventType.KeyDown) && !frameInput.HasKeyEvent(KeyCode.P, KeyEventType.IsRepeat);
        MTLCaptureManager cm = default;
        if (hasRequestedFrameCapture)
        {
            cm = MTLCaptureManager.SharedCaptureManager();
            var desc = new MTLCaptureDescriptor();
            desc.CaptureObject = _device;
            string captureFileName = $"capture_{frameInput.Frame}.gputrace";
            Console.WriteLine($"Capturing trace to {captureFileName}");
            desc.OutputURL = NSURL.FileURLWithPath(StringHelper.NSString(captureFileName));
            desc.Destination = MTLCaptureDestination.GPUTraceDocument;
            NSError error = default;
            cm.StartCapture(desc, ref error);
            if (error.Code != 0)
            {
                Console.WriteLine(StringHelper.String(error.LocalizedDescription));
            }
        }

        _renderPasses.Clear();
        _renderPasses.Add(_geometryPass);

        // configure the blit
        _blitPass.Destination = view.CurrentDrawable.Texture;
        _blitPass.Source = GBufferA;
        _renderPasses.Add(_blitPass);

        foreach (var pass in _renderPasses)
        {
            pass.Execute();
        }

        // present the output
        var presentCommandBuffer = _queue.CommandBuffer();
        presentCommandBuffer.Label = StringHelper.NSString("Present Command Buffer");
        presentCommandBuffer.PresentDrawable(view.CurrentDrawable);
        presentCommandBuffer.Commit();

        if (cm.NativePtr != nint.Zero && cm.IsCapturing)
        {
            cm.StopCapture();
        }
    }

    private void CreateGBuffer(ulong width, ulong height)
    {
        var gBufferDescriptor = new MTLTextureDescriptor()
        {
            Width = width,
            Height = height,
            MipmapLevelCount = 1,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
        };

        gBufferDescriptor.PixelFormat = kGBufferAFormat;
        var gBufferA = _device.NewTexture(gBufferDescriptor);
        gBufferA.Label = StringHelper.NSString("GBufferA");
        GBufferA = gBufferA;

        gBufferDescriptor.PixelFormat = kGBufferBFormat;
        var gBufferB = _device.NewTexture(gBufferDescriptor);
        gBufferB.Label = StringHelper.NSString("GBufferB");
        GBufferB = gBufferB;

        gBufferDescriptor.PixelFormat = kDepthFormat;
        var gBufferDepth = _device.NewTexture(gBufferDescriptor);
        gBufferDepth.Label = StringHelper.NSString("Depth");
        GBufferDepth = gBufferDepth;

        var depthStencilDesc = new MTLTextureDescriptor()
        {
            Width = width,
            Height = height,
            MipmapLevelCount = 1,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
            PixelFormat = MTLPixelFormat.Depth32FloatStencil8,
        };
        var depthStencil = _device.NewTexture(depthStencilDesc);
        depthStencil.Label = StringHelper.NSString("Depth/Stencil");
        DepthStencil = depthStencil;
    }
}
