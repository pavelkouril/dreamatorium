using System.Numerics;
using System.Runtime.InteropServices;
using Dreamatorium.Platforms.macOS;
using Dreamatorium.Scene;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using Resources_Mesh = Dreamatorium.Rendering.Resources.Mesh;

namespace Dreamatorium.Rendering;

public class GeometryPass : IPass<GeometryPassSettings>
{
    private readonly RenderingPipeline _pipeline;
    private readonly List<Resources_Mesh> _scene;
    private readonly Camera _camera;
    private MTLCommandQueue _queue;
    private MTLBuffer[] _frameData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];

    private MTLRenderPassDescriptor _gBufferPassDescriptor;

    public MTLRenderPipelineState _gBufferGenerationPipelineState;
    public MTLDepthStencilState _gBufferGenerationDepthStencilState;

    public GeometryPassSettings Settings { get; } = new();

    public GeometryPass(MTLDevice device, MTLCommandQueue queue, RenderingPipeline pipeline, List<Resources_Mesh> scene, Camera camera)
    {
        _pipeline = pipeline;
        _scene = scene;
        _camera = camera;
        _queue = queue;

        _gBufferPassDescriptor = new MTLRenderPassDescriptor();
        _gBufferPassDescriptor.DepthAttachment = new MTLRenderPassDepthAttachmentDescriptor()
        {
            LoadAction = MTLLoadAction.Clear,
            StoreAction = MTLStoreAction.Store,
        };

        _gBufferGenerationPipelineState = makeRenderPipelineState(device, "GBuffer Generation Stage", descriptor =>
        {
            var vertexDescriptor = new VertexDescriptors();

            descriptor.VertexFunction = ShaderLibrary.GetOrCreate(device).NewFunction(StringHelper.NSString("gbuffer_vertex"));
            descriptor.FragmentFunction = ShaderLibrary.GetOrCreate(device).NewFunction(StringHelper.NSString("gbuffer_fragment"));
            descriptor.VertexDescriptor = vertexDescriptor.Basic;
            descriptor.DepthAttachmentPixelFormat = MTLPixelFormat.Depth32Float;

            SetPixelFormat(descriptor, 0, RenderingPipeline.kGBufferAFormat);
            SetPixelFormat(descriptor, 1, RenderingPipeline.kGBufferBFormat);
            SetPixelFormat(descriptor, 2, RenderingPipeline.kDepthFormat);
        });

        _gBufferGenerationDepthStencilState = makeDepthStencilState(device, StringHelper.NSString("GBuffer Generation Stage"), descriptor =>
        {
            descriptor.IsDepthWriteEnabled = true;
            descriptor.DepthCompareFunction = MTLCompareFunction.Less;
        });

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<FrameData>(), MTLResourceOptions.ResourceStorageModeShared);
        }
    }

    private static void SetPixelFormat(MTLRenderPipelineDescriptor descriptor, ulong index, MTLPixelFormat pixelFormat)
    {
        var attach = descriptor.ColorAttachments.Object(index);
        attach.PixelFormat = pixelFormat;
        descriptor.ColorAttachments.SetObject(attach, index);
    }

    public void Execute()
    {
        var commandBuffer = _queue.CommandBuffer();
        commandBuffer.Label = StringHelper.NSString("GBuffer Commands");

        var cA0 = _gBufferPassDescriptor.ColorAttachments.Object(0);
        cA0.Texture = _pipeline.GBufferA;
        var cA1 = _gBufferPassDescriptor.ColorAttachments.Object(1);
        cA1.Texture = _pipeline.GBufferB;
        var cA2 = _gBufferPassDescriptor.ColorAttachments.Object(2);
        cA2.Texture = _pipeline.GBufferDepth;

        var dA = _gBufferPassDescriptor.DepthAttachment;
        dA.Texture = _pipeline.Depth;

        var renderEncoder = commandBuffer.RenderCommandEncoder(_gBufferPassDescriptor);
        renderEncoder.Label = StringHelper.NSString("BasePass");

        renderEncoder.SetRenderPipelineState(_gBufferGenerationPipelineState);
        renderEncoder.SetDepthStencilState(_gBufferGenerationDepthStencilState);

        var frameDataBuffer = _frameData[_pipeline.Frame];

        var modelMatrix = Matrix4x4.Identity;
        var normalMatrix = CreateNormalMatrix(modelMatrix);

        unsafe
        {
            FrameData* pFrameData = (FrameData*)frameDataBuffer.Contents.ToPointer();
            pFrameData->ModelMatrix = modelMatrix;
            pFrameData->ViewMatrix = _camera.WorldToCameraMatrix;
            pFrameData->ProjectionMatrix = _camera.ProjectionMatrix;
            pFrameData->NormalMatrix = normalMatrix;
        }

        renderEncoder.PushDebugGroup(StringHelper.NSString("Set Frame Data"));

        renderEncoder.SetVertexBuffer(frameDataBuffer, offset: 0, index: 5);
        renderEncoder.SetFragmentBuffer(frameDataBuffer, offset: 0, index: 5);

        renderEncoder.PopDebugGroup();

        foreach (var matGrouping in _scene.GroupBy(x => x.Material.Index))
        {
            renderEncoder.PushDebugGroup(StringHelper.NSString($"Material {matGrouping.Key}"));

            var material = matGrouping.First().Material;
            renderEncoder.SetFragmentTexture(material.Albedo, 0);
            renderEncoder.SetFragmentTexture(material.Normals, 1);
            renderEncoder.SetFragmentTexture(material.Opacity, 2);
            renderEncoder.SetFragmentTexture(material.Roughness, 3);
            renderEncoder.SetFragmentTexture(material.Metalness, 4);

            foreach (var mesh in matGrouping)
            {
                renderEncoder.PushDebugGroup(StringHelper.NSString($"Draw {mesh.Name}"));

                renderEncoder.SetVertexBuffer(mesh._vertexPositionsBuffer, offset: 0, 0);
                renderEncoder.SetVertexBuffer(mesh._vertexNormalsBuffer, offset: 0, 1);
                renderEncoder.SetVertexBuffer(mesh._vertexTangentsBuffer, offset: 0, 2);
                renderEncoder.SetVertexBuffer(mesh._vertexBitangentsBuffer, offset: 0, 3);
                renderEncoder.SetVertexBuffer(mesh._vertexTextureCoordinatesBuffer, offset: 0, 4);

                renderEncoder.DrawIndexedPrimitives(primitiveType: MTLPrimitiveType.Triangle,
                    indexCount: mesh._indexBuffer.Length / 4,
                    indexType: MTLIndexType.UInt32,
                    indexBuffer: mesh._indexBuffer,
                    indexBufferOffset: 0,
                    instanceCount: 1);

                renderEncoder.PopDebugGroup();
            }

            renderEncoder.PopDebugGroup();
        }

        renderEncoder.EndEncoding();

        commandBuffer.Commit();
    }

    private MTLRenderPipelineState makeRenderPipelineState(MTLDevice device, string label, Action<MTLRenderPipelineDescriptor> block)
    {
        var descriptor = new MTLRenderPipelineDescriptor();
        block(descriptor);
        descriptor.Label = StringHelper.NSString(label);
        NSError error = default;
        var state = device.NewRenderPipelineState(descriptor, ref error);
        if (error.NativePtr != nint.Zero)
        {
            Console.Error.WriteLine(StringHelper.String(error.LocalizedDescription));
        }

        return state;
    }

    private MTLDepthStencilState makeDepthStencilState(MTLDevice device, NSString label, Action<MTLDepthStencilDescriptor> block)
    {
        var descriptor = new MTLDepthStencilDescriptor();
        block(descriptor);
        descriptor.Label = label;
        return device.NewDepthStencilState(descriptor);
    }

    private static Matrix4x4 CreateNormalMatrix(Matrix4x4 modelMatrix)
    {
        if (!Matrix4x4.Invert(modelMatrix, out Matrix4x4 inverseModelMatrix))
        {
            return Matrix4x4.Identity;
        }

        Matrix4x4 normalMatrix = Matrix4x4.Transpose(inverseModelMatrix);
        normalMatrix.M14 = 0.0f;
        normalMatrix.M24 = 0.0f;
        normalMatrix.M34 = 0.0f;
        normalMatrix.M41 = 0.0f;
        normalMatrix.M42 = 0.0f;
        normalMatrix.M43 = 0.0f;
        normalMatrix.M44 = 1.0f;
        return normalMatrix;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FrameData
    {
        public Matrix4x4 ModelMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 NormalMatrix;
    }
}
