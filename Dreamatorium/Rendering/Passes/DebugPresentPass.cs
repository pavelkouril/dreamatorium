using System.Runtime.InteropServices;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct DebugPresentData
{
    public uint ChannelMode;
    public uint _pad0;
    public uint _pad1;
    public uint _pad2;
}

public class DebugPresentPass : IPass
{
    private MTLDevice _device;
    private readonly RenderingPipeline _pipeline;

    private readonly MTLRenderPipelineState _state;
    private readonly MTLBuffer _quadVertexBuffer;
    private readonly MTLBuffer[] _frameData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];
    private readonly MTL4RenderPassDescriptor _renderPassDescriptor;
    private MTL4ArgumentTable _vertexArgs;
    private MTL4ArgumentTable _fragmentArgs;

    public MTLTexture OutputTexture { get; private set; }

    public DebugPresentPass(MTLDevice device, RenderingPipeline pipeline)
    {
        _device = device;
        _pipeline = pipeline;

        OutputTexture = CreateOutputTexture(pipeline.GBufferA.Width, pipeline.GBufferA.Height);

        _state = makeRenderPipelineState("DebugPresentPass.State", descriptor =>
        {
            var library = ShaderLibrary.GetOrCreate(device);
            descriptor.VertexFunction = library.NewFunction(StringHelper.NSString("quad_vs"));
            descriptor.FragmentFunction = library.NewFunction(StringHelper.NSString("debug_present_frag"));

            var c0 = descriptor.ColorAttachments.Object(0);
            c0.PixelFormat = MTLPixelFormat.RGBA8Unorm;
            descriptor.ColorAttachments.SetObject(c0, 0);
        });

        var quadVertices = new[]
        {
            new System.Numerics.Vector2(-1, -1),
            new System.Numerics.Vector2(-1, 1),
            new System.Numerics.Vector2(1, -1),
            new System.Numerics.Vector2(1, -1),
            new System.Numerics.Vector2(-1, 1),
            new System.Numerics.Vector2(1, 1),
        };

        var quadBufferSize = (ulong)(quadVertices.Length * Marshal.SizeOf<System.Numerics.Vector2>());
        _quadVertexBuffer = device.NewBuffer(quadBufferSize, MTLResourceOptions.ResourceStorageModeShared);
        quadVertices.CopyToBuffer(_quadVertexBuffer);

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<DebugPresentData>(), MTLResourceOptions.ResourceStorageModeShared);
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
            MaxBufferBindCount = 1,
            MaxSamplerStateBindCount = 0,
            MaxTextureBindCount = 1,
            SupportAttributeStrides = false,
        }, ref argsError);

        _renderPassDescriptor = new MTL4RenderPassDescriptor();
        var c0Attachment = _renderPassDescriptor.ColorAttachments.Object(0);
        c0Attachment.Texture = OutputTexture;
    }

    public void Execute(MTL4CommandBuffer commandBuffer)
    {
        if (!_pipeline.TryGetActiveBufferVisualization(out MTLTexture sourceTexture, out BufferVisualizationChannels channels))
        {
            return;
        }

        var renderEncoder = commandBuffer.RenderCommandEncoder(_renderPassDescriptor);
        renderEncoder.Label = StringHelper.NSString("DebugPresentPass/Encoder");

        renderEncoder.SetRenderPipelineState(_state);
        _vertexArgs.SetAddress(_quadVertexBuffer.GpuAddress, 0);
        renderEncoder.SetArgumentTable(_vertexArgs, MTLRenderStages.RenderStageVertex);

        var frameDataBuffer = _frameData[_pipeline.Frame];
        FillData(frameDataBuffer, channels);
        _fragmentArgs.SetAddress(frameDataBuffer.GpuAddress, 0);
        _fragmentArgs.SetTexture(sourceTexture.GpuResourceID, 0);
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

        OutputTexture = CreateOutputTexture(width, height);
        var c0Attachment = _renderPassDescriptor.ColorAttachments.Object(0);
        c0Attachment.Texture = OutputTexture;
        _renderPassDescriptor.ColorAttachments.SetObject(c0Attachment, 0);
    }

    private MTLTexture CreateOutputTexture(ulong width, ulong height)
    {
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
        outputTexture.Label = StringHelper.NSString("DebugPresentPass.Output");
        return outputTexture;
    }

    public void AddResidencyAllocations(MTLResidencySet residencySet)
    {
        if (_quadVertexBuffer.NativePtr != nint.Zero)
        {
            residencySet.AddAllocation(new MTLAllocation(_quadVertexBuffer.NativePtr));
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

    private unsafe void FillData(MTLBuffer frameDataBuffer, BufferVisualizationChannels channels)
    {
        DebugPresentData* pData = (DebugPresentData*)frameDataBuffer.Contents.ToPointer();
        pData->ChannelMode = channels == BufferVisualizationChannels.A ? 1u : 0u;
    }
}
