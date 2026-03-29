using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class SkyboxPass : IPass
{
    private MTLDevice _device;

    private readonly RenderingPipeline _pipeline;
    private readonly LightingPass _lightingPass;

    private MTLTexture _skyboxTexture;

    private MTL4RenderPassDescriptor _skyboxPassDescriptor;

    private MTLRenderPipelineState _skyboxPipelineState;
    private MTLDepthStencilState _skyboxDepthStencilState;
    private MTL4ArgumentTable _vertexArgs;
    private MTL4ArgumentTable _fragmentArgs;

    public SkyboxPass(MTLDevice device, RenderingPipeline pipeline, MTLTexture skyboxTexture, LightingPass lightingPass)
    {
        _device = device;
        _pipeline = pipeline;
        _lightingPass = lightingPass;
        _skyboxTexture = skyboxTexture;

        _skyboxDepthStencilState = _device.NewDepthStencilState(new MTLDepthStencilDescriptor()
        {
            DepthCompareFunction = MTLCompareFunction.LessEqual,
            IsDepthWriteEnabled = false,
        });

        _skyboxPipelineState = MakeRenderPipelineState("Skybox", descriptor =>
        {
            var library = ShaderLibrary.GetOrCreate(device);
            descriptor.VertexFunction = library.NewFunction(StringHelper.NSString("skybox_vs"));
            descriptor.FragmentFunction = library.NewFunction(StringHelper.NSString("skybox_frag"));
            descriptor.DepthAttachmentPixelFormat = MTLPixelFormat.Depth32Float;
            descriptor.StencilAttachmentPixelFormat = MTLPixelFormat.Invalid;

            var c0 = descriptor.ColorAttachments.Object(0);
            c0.PixelFormat = MTLPixelFormat.RGBA8Unorm;
        });

        NSError argumentTableError = default;
        _vertexArgs = device.NewArgumentTable(new MTL4ArgumentTableDescriptor
        {
            MaxBufferBindCount = 1,
            MaxSamplerStateBindCount = 0,
            MaxTextureBindCount = 0,
            SupportAttributeStrides = false,
        }, ref argumentTableError);

        argumentTableError = default;
        _fragmentArgs = device.NewArgumentTable(new MTL4ArgumentTableDescriptor
        {
            MaxBufferBindCount = 0,
            MaxSamplerStateBindCount = 0,
            MaxTextureBindCount = 1,
            SupportAttributeStrides = false,
        }, ref argumentTableError);

        _skyboxPassDescriptor = new MTL4RenderPassDescriptor();
        var c0 = _skyboxPassDescriptor.ColorAttachments.Object(0);
        c0.Texture = lightingPass.OutputTexture;
        c0.LoadAction = MTLLoadAction.Load;
        c0.StoreAction = MTLStoreAction.Store;
        _skyboxPassDescriptor.ColorAttachments.SetObject(c0, 0);

        var dA = _skyboxPassDescriptor.DepthAttachment;
        dA.LoadAction = MTLLoadAction.Load;
        dA.StoreAction = MTLStoreAction.DontCare;
        dA.Texture = _pipeline.Depth;
    }

    public void Execute(MTL4CommandBuffer commandBuffer)
    {
        var c0 = _skyboxPassDescriptor.ColorAttachments.Object(0);
        c0.Texture = _lightingPass.OutputTexture;

        var dA = _skyboxPassDescriptor.DepthAttachment;
        dA.Texture = _pipeline.Depth;

        var renderEncoder = commandBuffer.RenderCommandEncoder(_skyboxPassDescriptor);
        renderEncoder.SetRenderPipelineState(_skyboxPipelineState);
        renderEncoder.SetDepthStencilState(_skyboxDepthStencilState);
        renderEncoder.SetCullMode(MTLCullMode.Front);
        _vertexArgs.SetAddress(_pipeline.CurrentFrameData.GpuAddress, 0);
        renderEncoder.SetArgumentTable(_vertexArgs, MTLRenderStages.RenderStageVertex);
        _fragmentArgs.SetTexture(_skyboxTexture.GpuResourceID, 0);
        renderEncoder.SetArgumentTable(_fragmentArgs, MTLRenderStages.RenderStageFragment);
        renderEncoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, 36);
        renderEncoder.EndEncoding();
    }

    public void AddResidencyAllocations(MTLResidencySet residencySet)
    {
        if (_skyboxTexture.NativePtr != nint.Zero)
        {
            residencySet.AddAllocation(new MTLAllocation(_skyboxTexture.NativePtr));
        }
    }

    private MTLRenderPipelineState MakeRenderPipelineState(string label, Action<MTLRenderPipelineDescriptor> block)
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
}
