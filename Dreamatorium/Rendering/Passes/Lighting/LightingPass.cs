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

public class LightingPass : IPass<LightingPassSettings>
{
    private MTLDevice _device;

    private readonly RenderingPipeline _pipeline;
    private readonly ShadowPass _shadowPass;

    private MTLRenderPipelineState _state;

    private MTLBuffer quadVertexBuffer;

    private MTLBuffer[] _frameData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];
    private MTL4ArgumentTable _vertexArgs;
    private MTL4ArgumentTable _fragmentArgs;

    public MTLTexture OutputTexture { get; private set; }

    private MTL4RenderPassDescriptor _renderPassDescriptor;

    public Vector3 LightDirection { get; set; }

    public LightingPassSettings Settings { get; } = new();

    public LightingPass(MTLDevice device, RenderingPipeline pipeline, ShadowPass shadowPass)
    {
        _device = device;
        _pipeline = pipeline;
        _shadowPass = shadowPass;

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
        quadVertexBuffer = device.NewBuffer(quadBufferSize, MTLResourceOptions.ResourceStorageModeShared);
        quadVertices.CopyToBuffer(quadVertexBuffer);

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<LightingData>(), MTLResourceOptions.ResourceStorageModeShared);
        }

        NSError argsError = default;
        _vertexArgs = device.NewArgumentTable(new MTL4ArgumentTableDescriptor
        {
            MaxBufferBindCount = 1,
            MaxSamplerStateBindCount = 0,
            MaxTextureBindCount = 0,
            SupportAttributeStrides = false,
        }, ref argsError);

        argsError = default;
        _fragmentArgs = device.NewArgumentTable(new MTL4ArgumentTableDescriptor
        {
            MaxBufferBindCount = 2,
            MaxSamplerStateBindCount = 0,
            MaxTextureBindCount = 4,
            SupportAttributeStrides = false,
        }, ref argsError);

        _renderPassDescriptor = new MTL4RenderPassDescriptor();
        var c0 = _renderPassDescriptor.ColorAttachments.Object(0);
        c0.Texture = OutputTexture;
    }

    public void Execute(MTL4CommandBuffer commandBuffer)
    {
        var renderEncoder = commandBuffer.RenderCommandEncoder(_renderPassDescriptor);
        renderEncoder.Label = StringHelper.NSString("LightingPass/Encoder");

        renderEncoder.SetRenderPipelineState(_state);
        renderEncoder.SetCullMode(MTLCullMode.Back);

        _vertexArgs.SetAddress(quadVertexBuffer.GpuAddress, 0);
        renderEncoder.SetArgumentTable(_vertexArgs, MTLRenderStages.RenderStageVertex);

        _fragmentArgs.SetAddress(_pipeline.CurrentFrameData.GpuAddress, 0);
        var frameDataBuffer = _frameData[_pipeline.Frame];
        FillData(frameDataBuffer);
        _fragmentArgs.SetAddress(frameDataBuffer.GpuAddress, 1);

        _fragmentArgs.SetTexture(_pipeline.GBufferA.GpuResourceID, 0);
        _fragmentArgs.SetTexture(_pipeline.GBufferB.GpuResourceID, 1);
        _fragmentArgs.SetTexture(_pipeline.Depth.GpuResourceID, 2);
        _fragmentArgs.SetTexture(_shadowPass.ShadowMap.GpuResourceID, 3);
        renderEncoder.SetArgumentTable(_fragmentArgs, MTLRenderStages.RenderStageFragment);

        renderEncoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, 6);

        renderEncoder.EndEncoding();
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

    public void AddResidencyAllocations(MTLResidencySet residencySet)
    {
        if (quadVertexBuffer.NativePtr != nint.Zero)
        {
            residencySet.AddAllocation(new MTLAllocation(quadVertexBuffer.NativePtr));
        }

        for (int i = 0; i < _frameData.Length; i++)
        {
            if (_frameData[i].NativePtr != nint.Zero)
            {
                residencySet.AddAllocation(new MTLAllocation(_frameData[i].NativePtr));
            }
        }
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

    protected unsafe void FillData(MTLBuffer frameDataBuffer)
    {
        LightingData* pData = (LightingData*)frameDataBuffer.Contents.ToPointer();
        pData->Position = Vector4.Zero;
        pData->ColorIntensity = new Vector4(1, 1, 1, 25);
        pData->Type = 0;
        pData->Direction = LightDirection.AsVector4();
    }
}
