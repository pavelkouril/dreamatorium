using System.Numerics;
using System.Runtime.InteropServices;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using Resources_Mesh = Dreamatorium.Rendering.Resources.Mesh;

namespace Dreamatorium.Rendering;

public class ShadowPass : IPass
{
    private const ulong kShadowMapSize = 2048;
    private readonly RenderingPipeline _pipeline;
    private readonly List<Resources_Mesh> _scene;
    private MTLCommandQueue _queue;
    private readonly MTLRenderPassDescriptor _shadowPassDescriptor;
    private readonly MTLRenderPipelineState _shadowPipelineState;
    private readonly MTLDepthStencilState _shadowDepthStencilState;
    private readonly MTLBuffer[] _frameData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];

    public MTLTexture ShadowMap { get; private set; }

    public ShadowPass(MTLDevice device, MTLCommandQueue queue, RenderingPipeline pipeline, List<Resources_Mesh> scene)
    {
        _pipeline = pipeline;
        _scene = scene;
        _queue = queue;

        var shadowTextureDescriptor = new MTLTextureDescriptor()
        {
            Width = kShadowMapSize,
            Height = kShadowMapSize,
            TextureType = MTLTextureType.Type2D,
            MipmapLevelCount = 1,
            PixelFormat = MTLPixelFormat.Depth32Float,
            Usage = MTLTextureUsage.RenderTarget | MTLTextureUsage.ShaderRead,
            StorageMode = MTLStorageMode.Private,
        };
        var shadowMap = device.NewTexture(shadowTextureDescriptor);
        shadowMap.Label = StringHelper.NSString("ShadowPass.ShadowMap");
        ShadowMap = shadowMap;

        var vertexDescriptor = new MTLVertexDescriptor();
        var positionAttribute = vertexDescriptor.Attributes.Object(0);
        positionAttribute.Format = MTLVertexFormat.Float3;
        positionAttribute.Offset = 0;
        positionAttribute.BufferIndex = 0;

        var uvAttribute = vertexDescriptor.Attributes.Object(4);
        uvAttribute.Format = MTLVertexFormat.Float3;
        uvAttribute.Offset = 0;
        uvAttribute.BufferIndex = 4;

        var positionLayout = vertexDescriptor.Layouts.Object(0);
        positionLayout.Stride = 12;

        var uvLayout = vertexDescriptor.Layouts.Object(4);
        uvLayout.Stride = 12;

        var shadowPipelineDescriptor = new MTLRenderPipelineDescriptor();
        shadowPipelineDescriptor.Label = StringHelper.NSString("ShadowPass.Pipeline");
        shadowPipelineDescriptor.VertexDescriptor = vertexDescriptor;
        shadowPipelineDescriptor.VertexFunction = ShaderLibrary.GetOrCreate(device).NewFunction(StringHelper.NSString("shadow_vertex"));
        shadowPipelineDescriptor.FragmentFunction = ShaderLibrary.GetOrCreate(device).NewFunction(StringHelper.NSString("shadow_fragment"));
        shadowPipelineDescriptor.DepthAttachmentPixelFormat = MTLPixelFormat.Depth32Float;
        shadowPipelineDescriptor.StencilAttachmentPixelFormat = MTLPixelFormat.Invalid;
        NSError shadowPipelineError = default;
        _shadowPipelineState = device.NewRenderPipelineState(shadowPipelineDescriptor, ref shadowPipelineError);
        if (shadowPipelineError.NativePtr != nint.Zero)
        {
            Console.Error.WriteLine($"Failed creating shadow pipeline state. Reason: {StringHelper.String(shadowPipelineError.LocalizedDescription)}");
        }

        var shadowDepthDescriptor = new MTLDepthStencilDescriptor
        {
            Label = StringHelper.NSString("ShadowPass.DepthStencil"),
            DepthCompareFunction = MTLCompareFunction.Less,
            IsDepthWriteEnabled = true,
        };
        _shadowDepthStencilState = device.NewDepthStencilState(shadowDepthDescriptor);

        _shadowPassDescriptor = new MTLRenderPassDescriptor();
        _shadowPassDescriptor.DepthAttachment = new MTLRenderPassDepthAttachmentDescriptor
        {
            LoadAction = MTLLoadAction.Clear,
            StoreAction = MTLStoreAction.Store,
            ClearDepth = 1.0,
            Texture = ShadowMap,
        };

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<FrameData>(), MTLResourceOptions.ResourceStorageModeShared);
        }
    }

    public void Execute(MTLCommandBuffer commandBuffer)
    {
        var renderEncoder = commandBuffer.RenderCommandEncoder(_shadowPassDescriptor);
        renderEncoder.Label = StringHelper.NSString("ShadowPass.Encoder");
        renderEncoder.SetViewport(new MTLViewport
        {
            originX = 0,
            originY = 0,
            width = kShadowMapSize,
            height = kShadowMapSize,
            znear = 0,
            zfar = 1,
        });
        renderEncoder.SetRenderPipelineState(_shadowPipelineState);
        renderEncoder.SetDepthStencilState(_shadowDepthStencilState);
        renderEncoder.SetCullMode(MTLCullMode.Back);

        var frameDataBuffer = _frameData[_pipeline.Frame];
        var modelMatrix = Matrix4x4.Identity;

        unsafe
        {
            FrameData* pFrameData = (FrameData*)frameDataBuffer.Contents.ToPointer();
            pFrameData->ModelMatrix = modelMatrix;
            var pipelineFrameData = (RenderingPipeline.FrameData*)_pipeline.CurrentFrameData.Contents.ToPointer();
            pFrameData->ViewMatrix = pipelineFrameData->LightViewMatrix;
            pFrameData->ProjectionMatrix = pipelineFrameData->LightProjectionMatrix;
        }

        renderEncoder.SetVertexBuffer(frameDataBuffer, offset: 0, index: 5);

        foreach (var matGrouping in _scene.GroupBy(x => x.Material.Index))
        {
            var material = matGrouping.First().Material;
            renderEncoder.SetFragmentTexture(material.Opacity, 0);

            foreach (var mesh in matGrouping)
            {
                renderEncoder.SetVertexBuffer(mesh._vertexPositionsBuffer, offset: 0, index: 0);
                renderEncoder.SetVertexBuffer(mesh._vertexTextureCoordinatesBuffer, offset: 0, index: 4);

                renderEncoder.DrawIndexedPrimitives(primitiveType: MTLPrimitiveType.Triangle,
                    indexCount: mesh._indexBuffer.Length / 4,
                    indexType: MTLIndexType.UInt32,
                    indexBuffer: mesh._indexBuffer,
                    indexBufferOffset: 0,
                    instanceCount: 1);
            }
        }

        renderEncoder.EndEncoding();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameData
    {
        public Matrix4x4 ModelMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
    }
}
