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
    private MTLBuffer[] _frameData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];

    private MTL4RenderPassDescriptor _gBufferPassDescriptor;

    public MTLRenderPipelineState _gBufferGenerationPipelineState;
    public MTLDepthStencilState _gBufferGenerationDepthStencilState;

    private MTL4ArgumentTable _vertexArgs;
    private MTL4ArgumentTable _fragmentArgs;

    public GeometryPassSettings Settings { get; } = new();

    public GeometryPass(MTLDevice device, RenderingPipeline pipeline, List<Resources_Mesh> scene, Camera camera)
    {
        _pipeline = pipeline;
        _scene = scene;
        _camera = camera;

        _gBufferPassDescriptor = new MTL4RenderPassDescriptor();
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

        var atDesc = new MTL4ArgumentTableDescriptor
        {
            MaxBufferBindCount = 6, // pos, norm, tan, bitan, uv, frameData
            MaxTextureBindCount = 0,
            SupportAttributeStrides = false
        };
        NSError err = default;
        _vertexArgs = device.NewArgumentTable(atDesc, ref err);

        atDesc.MaxBufferBindCount = 0;
        atDesc.MaxTextureBindCount = 5; // albedo, normals, opacity, roughness, metalness
        _fragmentArgs = device.NewArgumentTable(atDesc, ref err);
    }

    private static void SetPixelFormat(MTLRenderPipelineDescriptor descriptor, ulong index, MTLPixelFormat pixelFormat)
    {
        var attach = descriptor.ColorAttachments.Object(index);
        attach.PixelFormat = pixelFormat;
        descriptor.ColorAttachments.SetObject(attach, index);
    }

    public void Execute(MTL4CommandBuffer commandBuffer)
    {
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

        _vertexArgs.SetAddress(frameDataBuffer.GpuAddress, 5);

        renderEncoder.PopDebugGroup();

        foreach (var matGrouping in _scene.GroupBy(x => x.Material.Index))
        {
            renderEncoder.PushDebugGroup(StringHelper.NSString($"Material {matGrouping.Key}"));

            var material = matGrouping.First().Material;
            _fragmentArgs.SetTexture(material.Albedo.GpuResourceID, 0);
            _fragmentArgs.SetTexture(material.Normals.GpuResourceID, 1);
            _fragmentArgs.SetTexture(material.Opacity.GpuResourceID, 2);
            _fragmentArgs.SetTexture(material.Roughness.GpuResourceID, 3);
            _fragmentArgs.SetTexture(material.Metalness.GpuResourceID, 4);

            renderEncoder.SetArgumentTable(_fragmentArgs, MTLRenderStages.RenderStageFragment);

            foreach (var mesh in matGrouping)
            {
                renderEncoder.PushDebugGroup(StringHelper.NSString($"Draw {mesh.Name}"));

                _vertexArgs.SetAddress(mesh._vertexPositionsBuffer.GpuAddress, 0);
                _vertexArgs.SetAddress(mesh._vertexNormalsBuffer.GpuAddress, 1);
                _vertexArgs.SetAddress(mesh._vertexTangentsBuffer.GpuAddress, 2);
                _vertexArgs.SetAddress(mesh._vertexBitangentsBuffer.GpuAddress, 3);
                _vertexArgs.SetAddress(mesh._vertexTextureCoordinatesBuffer.GpuAddress, 4);

                renderEncoder.SetArgumentTable(_vertexArgs, MTLRenderStages.RenderStageVertex);

                renderEncoder.DrawIndexedPrimitives(primitiveType: MTLPrimitiveType.Triangle,
                    indexCount: mesh._indexBuffer.Length / 4,
                    indexType: MTLIndexType.UInt32,
                    indexBuffer: mesh._indexBuffer.GpuAddress,
                    indexBufferLength: mesh._indexBuffer.Length,
                    instanceCount: 1);

                renderEncoder.PopDebugGroup();
            }

            renderEncoder.PopDebugGroup();
        }

        renderEncoder.EndEncoding();
    }

    public void AddResidencyAllocations(MTLResidencySet residencySet)
    {
        for (int i = 0; i < _frameData.Length; i++)
        {
            if (_frameData[i].NativePtr != nint.Zero)
            {
                residencySet.AddAllocation(new MTLAllocation(_frameData[i].NativePtr));
            }
        }
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
