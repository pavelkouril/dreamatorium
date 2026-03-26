using System.Numerics;
using System.Runtime.InteropServices;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct LightingData
{
    public Vector4 Position;
    public Vector4 Direction;
    public Vector4 ColorIntensity;
    public uint Type;
    public uint _pad0;
    public uint _pad1;
    public uint _pad2;
}

public class LightingPass : IPass
{
    private MTLDevice _device;

    private readonly RenderingPipeline _pipeline;
    private readonly ShadowPass _shadowPass;

    private MTLCommandQueue _queue;

    private MTLRenderPipelineState _state;

    private MTLBuffer quadVertexBuffer;

    private MTLBuffer[] _frameData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];

    public MTLTexture OutputTexture { get; private set; }

    private MTLRenderPassDescriptor _renderPassDescriptor;

    public Vector3 LightDirection { get; set; }

    public LightingPass(MTLDevice device, MTLCommandQueue queue, RenderingPipeline pipeline, ShadowPass shadowPass)
    {
        _device = device;
        _pipeline = pipeline;
        _shadowPass = shadowPass;
        _queue = queue;

        var outputTextureDescriptor = new MTLTextureDescriptor()
        {
            Width = pipeline.GBufferA.Width,
            Height = pipeline.GBufferA.Height,
            MipmapLevelCount = 1,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
        };

        var outputTexture = _device.NewTexture(outputTextureDescriptor);
        outputTexture.Label = StringHelper.NSString("LightingPass.Output");
        OutputTexture = outputTexture;

        _state = makeRenderPipelineState("LightingPass.State", descriptor =>
        {
            var library = ShaderLibrary.GetOrCreate(device);
            descriptor.VertexFunction = library.NewFunction(StringHelper.NSString("quad_vs"));
            descriptor.FragmentFunction = library.NewFunction(StringHelper.NSString("lighting_frag"));

            var foo = descriptor.ColorAttachments.Object(0);
            foo.PixelFormat = MTLPixelFormat.RGBA8Unorm;
            descriptor.ColorAttachments.SetObject(foo, 0);
        });

        var quadVertices = new[]
        {
            new Vector2(-1, -1),
            new Vector2(-1, 1),
            new Vector2(1, -1),
            new Vector2(1, -1),
            new Vector2(-1, 1),
            new Vector2(1, 1),
        };

        var quadBufferSize = (ulong)(quadVertices.Length * Marshal.SizeOf<Vector2>());
        quadVertexBuffer = device.NewBuffer(quadBufferSize, MTLResourceOptions.ResourceStorageModeManaged);
        quadVertices.CopyToBuffer(quadVertexBuffer);
        quadVertexBuffer.DidModifyRange(new NSRange()
        {
            location = 0,
            length = quadBufferSize,
        });

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<LightingData>(), MTLResourceOptions.ResourceStorageModeManaged);
        }

        _renderPassDescriptor = new MTLRenderPassDescriptor();
        var c0 = _renderPassDescriptor.ColorAttachments.Object(0);
        c0.Texture = OutputTexture;
    }

    public void Execute()
    {
        var commandBuffer = _queue.CommandBuffer();
        commandBuffer.Label = StringHelper.NSString("FullScreenPass/CommandBuffer");

        var renderEncoder = commandBuffer.RenderCommandEncoder(_renderPassDescriptor);

        renderEncoder.Label = StringHelper.NSString("FullScreenPass/Encoder");

        renderEncoder.SetRenderPipelineState(_state);
        renderEncoder.SetCullMode(MTLCullMode.Back);

        renderEncoder.SetVertexBuffer(quadVertexBuffer, offset: 0, index: 0);
        renderEncoder.UseResource(quadVertexBuffer, MTLResourceUsage.Read);

        renderEncoder.SetFragmentBuffer(_pipeline.CurrentFrameData, offset: 0, index: 0);
        var frameDataBuffer = _frameData[_pipeline.Frame];
        FillData(frameDataBuffer);
        renderEncoder.SetFragmentBuffer(frameDataBuffer, offset: 0, index: 1);

        BindFragmentInputs(renderEncoder);

        renderEncoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, 6);

        renderEncoder.EndEncoding();

        commandBuffer.Commit();
    }

    public void Resize(ulong width, ulong height)
    {
        if (width == 0 || height == 0)
        {
            return;
        }

        if (OutputTexture.Width == width && OutputTexture.Height == height)
        {
            return;
        }

        var outputTextureDescriptor = new MTLTextureDescriptor()
        {
            Width = width,
            Height = height,
            MipmapLevelCount = 1,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.RenderTarget,
            StorageMode = MTLStorageMode.Shared,
            PixelFormat = MTLPixelFormat.RGBA8Unorm,
        };

        var outputTexture = _device.NewTexture(outputTextureDescriptor);
        outputTexture.Label = StringHelper.NSString("LightingPass.Output");
        OutputTexture = outputTexture;

        var c0 = _renderPassDescriptor.ColorAttachments.Object(0);
        c0.Texture = OutputTexture;
        _renderPassDescriptor.ColorAttachments.SetObject(c0, 0);
    }

    private MTLRenderPipelineState makeRenderPipelineState(string label, Action<MTLRenderPipelineDescriptor> block)
    {
        var descriptor = new MTLRenderPipelineDescriptor();
        block(descriptor);
        descriptor.Label = StringHelper.NSString(label);
        NSError error = default;
        var state = _device.NewRenderPipelineState(descriptor, ref error);
        if (error.Code != 0)
        {
            Console.WriteLine(StringHelper.String(error.LocalizedDescription));
        }
        return state;
    }

    protected void BindFragmentInputs(MTLRenderCommandEncoder renderEncoder)
    {
        renderEncoder.SetFragmentTexture(_pipeline.GBufferA, 0);
        renderEncoder.SetFragmentTexture(_pipeline.GBufferB, 1);
        renderEncoder.SetFragmentTexture(_pipeline.Depth, 2);
        renderEncoder.SetFragmentTexture(_shadowPass.ShadowMap, 3);
    }

    protected unsafe void FillData(MTLBuffer frameDataBuffer)
    {
        LightingData* pData = (LightingData*)frameDataBuffer.Contents.ToPointer();
        pData->Position = Vector4.Zero;
        pData->ColorIntensity = new Vector4(1, 1, 1, 25);
        pData->Type = 0;
        pData->Direction = LightDirection.AsVector4();
        frameDataBuffer.DidModifyRange(new NSRange
        {
            location = 0,
            length = (ulong)Marshal.SizeOf<LightingData>()
        });
    }
}
