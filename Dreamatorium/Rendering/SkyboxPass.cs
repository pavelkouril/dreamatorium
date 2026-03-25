using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class SkyboxPass : IPass
{
    private MTLDevice _device;

    private MTLCommandQueue _queue;

    private readonly RenderingPipeline _pipeline;
    private readonly LightingPass _lightingPass;

    private MTLTexture _skyboxTexture;

    private MTLRenderPassDescriptor _skyboxPassDescriptor;

    private MTLRenderPipelineState _skyboxPipelineState;
    private MTLDepthStencilState _skyboxDepthStencilState;

    public SkyboxPass(MTLDevice device, MTLCommandQueue queue, RenderingPipeline pipeline, MTLTexture skyboxTexture, LightingPass lightingPass)
    {
        _device = device;
        _queue = queue;
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

        _skyboxPassDescriptor = new MTLRenderPassDescriptor();
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

    public void Execute()
    {
        var c0 = _skyboxPassDescriptor.ColorAttachments.Object(0);
        c0.Texture = _lightingPass.OutputTexture;
        _skyboxPassDescriptor.ColorAttachments.SetObject(c0, 0);

        var dA = _skyboxPassDescriptor.DepthAttachment;
        dA.Texture = _pipeline.Depth;
        _skyboxPassDescriptor.DepthAttachment = dA;

        var commandBuffer = _queue.CommandBuffer();

        var renderEncoder = commandBuffer.RenderCommandEncoder(_skyboxPassDescriptor);
        renderEncoder.SetRenderPipelineState(_skyboxPipelineState);
        renderEncoder.SetDepthStencilState(_skyboxDepthStencilState);
        renderEncoder.SetCullMode(MTLCullMode.Front);
        renderEncoder.SetVertexBuffer(_pipeline.CurrentFrameData, offset: 0, index: 0);
        renderEncoder.SetFragmentTexture(_skyboxTexture, 0);
        renderEncoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, 36);
        renderEncoder.EndEncoding();
        commandBuffer.Commit();
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
