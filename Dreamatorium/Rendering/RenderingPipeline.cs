using System.Numerics;
using System.Runtime.InteropServices;
using Dreamatorium.Input;
using Dreamatorium.Rendering.Resources;
using Dreamatorium.Scene;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class RenderingPipeline
{
    public const int kMaxFramesInFlight = 3;

    public const MTLPixelFormat kGBufferAFormat = MTLPixelFormat.RGBA8Unorm;
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
    /// Clip space Depth
    /// </summary>
    public MTLTexture Depth { get; private set; }

    public MTLBuffer CurrentFrameData => _frameData[Frame];

    public int Frame { get; private set; }

    private readonly GeometryPass _geometryPass;
    private readonly BlitPass _blitPass;
    private readonly LightingPass _lightingPass;
    private readonly SkyboxPass _skyboxPass;
    private readonly ShadowPass _shadowPass;

    private MTLBuffer[] _frameData = new MTLBuffer[kMaxFramesInFlight];

    private MTLCommandQueue _queue;

    private readonly Camera _camera;

    public RenderingPipeline(MTLDevice device, List<Mesh> scene, MTLTexture skyboxTexture, Camera camera, ulong initialWidth, ulong initialHeight)
    {
        _device = device;
        _camera = camera;

        _queue = device.NewCommandQueue();

        CreateGBuffer(initialWidth, initialHeight);

        _geometryPass = new GeometryPass(_device, _queue, this, scene, camera);
        _blitPass = new BlitPass(_queue);
        _lightingPass = new LightingPass(_device, _queue, this);
        _skyboxPass = new SkyboxPass(_device, _queue, this, skyboxTexture, _lightingPass);

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<FrameData>(), MTLResourceOptions.ResourceStorageModeShared);
        }
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

        Frame = (Frame + 1) % kMaxFramesInFlight;
        var frameDataBuffer = _frameData[Frame];
        unsafe
        {
            FrameData* pFrameData = (FrameData*)frameDataBuffer.Contents.ToPointer();
            pFrameData->WorldSpaceCameraPosition = _camera.Position.AsVector4();
            pFrameData->ViewMatrix = _camera.WorldToCameraMatrix;
            pFrameData->ProjectionMatrix = _camera.ProjectionMatrix;
            if (Matrix4x4.Invert(_camera.WorldToCameraMatrix, out Matrix4x4 invViewMatrix))
            {
                pFrameData->InverseViewMatrix = invViewMatrix;
            }
            if (Matrix4x4.Invert(_camera.ProjectionMatrix, out Matrix4x4 invProjectionMatrix))
            {
                pFrameData->InverseProjectionMatrix = invProjectionMatrix;
            }
            pFrameData->ProjectionParameters = new Vector4(1, _camera.NearPlaneDistance, _camera.FarPlaneDistance, 1 / _camera.FarPlaneDistance);
            pFrameData->ViewProjectionMatrix = _camera.ProjectionMatrix * _camera.WorldToCameraMatrix;
            if (Matrix4x4.Invert(pFrameData->ViewProjectionMatrix, out Matrix4x4 invViewProjectionMatrix))
            {
                pFrameData->InverseViewProjectionMatrix = invViewProjectionMatrix;
            }
            pFrameData->ViewRotationMatrix = _camera.WorldToCameraMatrix;
            pFrameData->ViewRotationMatrix.Translation = Vector3.Zero;
        }

        _renderPasses.Clear();
        _renderPasses.Add(_geometryPass);

        _lightingPass.LightDirection = Vector3.Normalize(new Vector3(-1, 1, 1));
        _renderPasses.Add(_lightingPass);

        _renderPasses.Add(_shadowPass);

        _renderPasses.Add(_skyboxPass);

        var finalTexture = _lightingPass.OutputTexture;

        foreach (var pass in _renderPasses)
        {
            pass.Execute();
        }

        // present the output
        var presentCommandBuffer = _queue.CommandBuffer();
        presentCommandBuffer.Label = StringHelper.NSString("Present Command Buffer");

        var currentDrawable = view.CurrentDrawable;

        var blitEncoder = presentCommandBuffer.BlitCommandEncoder(new MTLBlitPassDescriptor());
        blitEncoder.Label = StringHelper.NSString("Blit/Encoder");
        blitEncoder.CopyFromTexture(finalTexture, currentDrawable.Texture);
        blitEncoder.EndEncoding();

        presentCommandBuffer.PresentDrawable(currentDrawable);
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
            PixelFormat = MTLPixelFormat.Depth32Float,
        };
        var depthStencil = _device.NewTexture(depthStencilDesc);
        depthStencil.Label = StringHelper.NSString("Depth/Stencil");
        Depth = depthStencil;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FrameData
    {
        public Vector4 ProjectionParameters;
        public Vector4 WorldSpaceCameraPosition;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 InverseViewMatrix;
        public Matrix4x4 InverseProjectionMatrix;
        public Matrix4x4 ViewProjectionMatrix;
        public Matrix4x4 InverseViewProjectionMatrix;
        public Matrix4x4 ViewRotationMatrix;
    }
}
