using System.Numerics;
using System.Runtime.InteropServices;
using Dreamatorium.Platforms.macOS;
using Dreamatorium.Rendering.Resources;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class ShadowPass : IPass
{
    private readonly MTLDevice _device;
    private readonly RenderingPipeline _pipeline;
    private readonly Scene _scene;

    private readonly MTLComputePipelineState _rtShadowPipeline;
    private readonly MTLBuffer[] _lightingData = new MTLBuffer[RenderingPipeline.kMaxFramesInFlight];
    private readonly MTL4ArgumentTable _computeArgs;

    private readonly List<MTL4PrimitiveAccelerationStructureDescriptor> _blasDescriptors = [];
    private readonly List<MTLAccelerationStructure> _blas = [];
    private readonly List<MTLBuffer> _blasScratchBuffers = [];

    private MTL4InstanceAccelerationStructureDescriptor _tlasDescriptor;
    private MTLAccelerationStructure _tlas;
    private MTLBuffer _tlasScratchBuffer;
    private MTLBuffer _instanceDescriptorBuffer;

    private bool _accelerationStructuresBuilt;

    [BufferVisualization("RT Shadows")]
    public MTLTexture ShadowMap { get; private set; }

    public Vector3 LightDirection { get; set; }

    public ShadowPass(MTLDevice device, RenderingPipeline pipeline, Scene scene)
    {
        _device = device;
        _pipeline = pipeline;
        _scene = scene;

        if (!_device.SupportsRaytracing)
        {
            throw new InvalidOperationException("This renderer now requires Metal ray tracing support for shadows.");
        }

        var library = ShaderLibrary.GetOrCreate(device);
        NSError pipelineError = default;
        _rtShadowPipeline = _device.NewComputePipelineState(library.NewFunction(StringHelper.NSString("rt_shadow_cs")), ref pipelineError);
        if (pipelineError.NativePtr != nint.Zero)
        {
            throw new Exception($"Failed creating RT shadow compute pipeline. Reason: {StringHelper.String(pipelineError.LocalizedDescription)}");
        }

        ShadowMap = CreateShadowTexture(pipeline.GBufferA.Width, pipeline.GBufferA.Height);

        for (int i = 0; i < _lightingData.Length; i++)
        {
            _lightingData[i] = _device.NewBuffer((ulong)Marshal.SizeOf<LightingData>(), MTLResourceOptions.ResourceStorageModeShared);
        }

        NSError argsError = default;
        _computeArgs = _device.NewArgumentTable(new MTL4ArgumentTableDescriptor
        {
            MaxBufferBindCount = 3,
            MaxSamplerStateBindCount = 0,
            MaxTextureBindCount = 3,
            SupportAttributeStrides = false,
        }, ref argsError);
        if (argsError.NativePtr != nint.Zero)
        {
            throw new Exception($"Failed creating RT shadow argument table. Reason: {StringHelper.String(argsError.LocalizedDescription)}");
        }

        BuildRayTracingSceneResources();
    }

    public void Execute(MTL4CommandBuffer commandBuffer)
    {
        Resize(_pipeline.GBufferA.Width, _pipeline.GBufferA.Height);

        if (!_accelerationStructuresBuilt)
        {
            BuildAccelerationStructures(commandBuffer);
            _accelerationStructuresBuilt = true;
        }

        var frameLightingBuffer = _lightingData[_pipeline.Frame];
        FillLightingData(frameLightingBuffer);

        var computeEncoder = commandBuffer.ComputeCommandEncoder;
        computeEncoder.Label = StringHelper.NSString("RTShadowPass/Trace");
        computeEncoder.SetComputePipelineState(_rtShadowPipeline);

        _computeArgs.SetAddress(_pipeline.CurrentFrameData.GpuAddress, 0);
        _computeArgs.SetAddress(frameLightingBuffer.GpuAddress, 1);
        _computeArgs.SetResource(_tlas.GpuResourceID, 2);
        _computeArgs.SetTexture(_pipeline.GBufferB.GpuResourceID, 0);
        _computeArgs.SetTexture(_pipeline.Depth.GpuResourceID, 1);
        _computeArgs.SetTexture(ShadowMap.GpuResourceID, 2);
        computeEncoder.SetArgumentTable(_computeArgs);

        ulong threadsPerGroupX = Math.Min(_rtShadowPipeline.ThreadExecutionWidth, 16UL);
        ulong threadsPerGroupY = Math.Max(1UL, Math.Min(_rtShadowPipeline.MaxTotalThreadsPerThreadgroup / threadsPerGroupX, 16UL));

        var threadsPerThreadgroup = new MTLSize
        {
            width = threadsPerGroupX,
            height = threadsPerGroupY,
            depth = 1,
        };

        var threadsPerGrid = new MTLSize
        {
            width = ShadowMap.Width,
            height = ShadowMap.Height,
            depth = 1,
        };

        computeEncoder.DispatchThreads(threadsPerGrid, threadsPerThreadgroup);
        computeEncoder.EndEncoding();
    }

    public void Resize(ulong width, ulong height)
    {
        if (width == 0 || height == 0)
        {
            return;
        }

        if (ShadowMap.Width == width && ShadowMap.Height == height)
        {
            return;
        }

        ShadowMap = CreateShadowTexture(width, height);
    }

    public void AddResidencyAllocations(MTLResidencySet residencySet)
    {
        for (int i = 0; i < _lightingData.Length; i++)
        {
            if (_lightingData[i].NativePtr != nint.Zero)
            {
                residencySet.AddAllocation(new MTLAllocation(_lightingData[i].NativePtr));
            }
        }

        if (_instanceDescriptorBuffer.NativePtr != nint.Zero)
        {
            residencySet.AddAllocation(new MTLAllocation(_instanceDescriptorBuffer.NativePtr));
        }

        if (_tlasScratchBuffer.NativePtr != nint.Zero)
        {
            residencySet.AddAllocation(new MTLAllocation(_tlasScratchBuffer.NativePtr));
        }

        if (_tlas.NativePtr != nint.Zero)
        {
            residencySet.AddAllocation(new MTLAllocation(_tlas.NativePtr));
        }

        foreach (var scratch in _blasScratchBuffers)
        {
            if (scratch.NativePtr != nint.Zero)
            {
                residencySet.AddAllocation(new MTLAllocation(scratch.NativePtr));
            }
        }

        foreach (var blas in _blas)
        {
            if (blas.NativePtr != nint.Zero)
            {
                residencySet.AddAllocation(new MTLAllocation(blas.NativePtr));
            }
        }
    }

    private MTLTexture CreateShadowTexture(ulong width, ulong height)
    {
        var textureDescriptor = new MTLTextureDescriptor
        {
            Width = width,
            Height = height,
            MipmapLevelCount = 1,
            TextureType = MTLTextureType.Type2D,
            Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.ShaderWrite,
            StorageMode = MTLStorageMode.Private,
            PixelFormat = MTLPixelFormat.R8Unorm,
        };

        var texture = _device.NewTexture(textureDescriptor);
        texture.Label = StringHelper.NSString("ShadowPass.RTShadowMask");
        return texture;
    }

    private void BuildRayTracingSceneResources()
    {
        for (int meshIndex = 0; meshIndex < _scene.Meshes.Count; meshIndex++)
        {
            Mesh mesh = _scene.Meshes[meshIndex];

            var geometryDescriptor = new MTL4AccelerationStructureTriangleGeometryDescriptor
            {
                VertexBuffer = new MTL4BufferRange
                {
                    bufferAddress = mesh._vertexPositionsBuffer.GpuAddress,
                    length = mesh._vertexPositionsBuffer.Length,
                },
                VertexStride = 12,
                VertexFormat = MTLAttributeFormat.Float3,
                IndexBuffer = new MTL4BufferRange
                {
                    bufferAddress = mesh._indexBuffer.GpuAddress,
                    length = mesh._indexBuffer.Length,
                },
                IndexType = MTLIndexType.UInt32,
                TriangleCount = mesh._indexBuffer.Length / 12,
                Opaque = true,
            };

            var geometryDescriptors = NSArray.Array(new NSObject(geometryDescriptor.NativePtr));
            var blasDescriptor = new MTL4PrimitiveAccelerationStructureDescriptor
            {
                GeometryDescriptors = geometryDescriptors,
                Usage = MTLAccelerationStructureUsage.PreferFastIntersection,
            };
            _blasDescriptors.Add(blasDescriptor);

            MTLAccelerationStructureSizes blasSizes = _device.AccelerationStructureSizes(new MTLAccelerationStructureDescriptor(blasDescriptor.NativePtr));
            var blas = _device.NewAccelerationStructure(blasSizes.accelerationStructureSize);
            blas.Label = StringHelper.NSString($"RTShadowPass.BLAS[{mesh.Name}]");
            _blas.Add(blas);

            var blasScratch = _device.NewBuffer(blasSizes.buildScratchBufferSize, MTLResourceOptions.ResourceStorageModePrivate);
            blasScratch.Label = StringHelper.NSString($"RTShadowPass.BLAS[{mesh.Name}].Scratch");
            _blasScratchBuffers.Add(blasScratch);
        }

        ulong instanceDescriptorSize = (ulong)Marshal.SizeOf<InstanceDescriptorRaw>();
        _instanceDescriptorBuffer = _device.NewBuffer((ulong)_scene.Meshes.Count * instanceDescriptorSize, MTLResourceOptions.ResourceStorageModeShared);
        _instanceDescriptorBuffer.Label = StringHelper.NSString("RTShadowPass.InstanceDescriptors");

        unsafe
        {
            var pInstances = (InstanceDescriptorRaw*)_instanceDescriptorBuffer.Contents.ToPointer();
            for (int i = 0; i < _scene.Meshes.Count; i++)
            {
                pInstances[i] = InstanceDescriptorRaw.Identity(_blas[i].GpuResourceID, (uint)i);
            }
        }

        _tlasDescriptor = new MTL4InstanceAccelerationStructureDescriptor
        {
            InstanceCount = (ulong)_scene.Meshes.Count,
            InstanceDescriptorType = MTLAccelerationStructureInstanceDescriptorType.Indirect,
            InstanceDescriptorStride = instanceDescriptorSize,
            InstanceTransformationMatrixLayout = MTLMatrixLayout.ColumnMajor,
            InstanceDescriptorBuffer = new MTL4BufferRange
            {
                bufferAddress = _instanceDescriptorBuffer.GpuAddress,
                length = _instanceDescriptorBuffer.Length,
            },
            Usage = MTLAccelerationStructureUsage.PreferFastIntersection,
        };

        MTLAccelerationStructureSizes tlasSizes = _device.AccelerationStructureSizes(new MTLAccelerationStructureDescriptor(_tlasDescriptor.NativePtr));
        _tlas = _device.NewAccelerationStructure(tlasSizes.accelerationStructureSize);
        _tlas.Label = StringHelper.NSString("RTShadowPass.TLAS");

        _tlasScratchBuffer = _device.NewBuffer(tlasSizes.buildScratchBufferSize, MTLResourceOptions.ResourceStorageModePrivate);
        _tlasScratchBuffer.Label = StringHelper.NSString("RTShadowPass.TLAS.Scratch");
    }

    private void BuildAccelerationStructures(MTL4CommandBuffer commandBuffer)
    {
        var buildEncoder = commandBuffer.ComputeCommandEncoder;
        buildEncoder.Label = StringHelper.NSString("RTShadowPass/BuildAS");

        for (int i = 0; i < _blas.Count; i++)
        {
            buildEncoder.BuildAccelerationStructure(_blas[i], _blasDescriptors[i], new MTL4BufferRange
            {
                bufferAddress = _blasScratchBuffers[i].GpuAddress,
                length = _blasScratchBuffers[i].Length,
            });
        }

        buildEncoder.BuildAccelerationStructure(_tlas, _tlasDescriptor, new MTL4BufferRange
        {
            bufferAddress = _tlasScratchBuffer.GpuAddress,
            length = _tlasScratchBuffer.Length,
        });

        buildEncoder.EndEncoding();
    }

    private unsafe void FillLightingData(MTLBuffer lightingDataBuffer)
    {
        LightingData* pData = (LightingData*)lightingDataBuffer.Contents.ToPointer();
        pData->Position = Vector4.Zero;
        pData->Direction = LightDirection.AsVector4();
        pData->ColorIntensity = new Vector4(1, 1, 1, 25);
        pData->Type = 0;
        pData->_pad0 = 0;
        pData->_pad1 = 0;
        pData->_pad2 = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct InstanceDescriptorRaw
    {
        public fixed float Transform[12];
        public uint Options;
        public uint Mask;
        public uint IntersectionFunctionTableOffset;
        public uint UserID;
        public MTLResourceID AccelerationStructureID;

        public static InstanceDescriptorRaw Identity(MTLResourceID accelerationStructureID, uint userID)
        {
            InstanceDescriptorRaw descriptor = default;
            descriptor.Transform[0] = 1.0f;
            descriptor.Transform[4] = 1.0f;
            descriptor.Transform[8] = 1.0f;
            descriptor.Options = (uint)MTLAccelerationStructureInstanceOptions.Opaque;
            descriptor.Mask = 0xFF;
            descriptor.IntersectionFunctionTableOffset = 0;
            descriptor.UserID = userID;
            descriptor.AccelerationStructureID = accelerationStructureID;
            return descriptor;
        }
    }
}
