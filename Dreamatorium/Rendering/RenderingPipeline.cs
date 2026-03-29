using System.Numerics;
using System.Runtime.InteropServices;
using System.Reflection;
using Dreamatorium.Rendering.Resources;
using Dreamatorium.Scene;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;

namespace Dreamatorium.Rendering;

public class RenderingPipeline
{
    public const int kMaxFramesInFlight = 3;

    public const MTLPixelFormat kGBufferAFormat = MTLPixelFormat.RGBA8Unorm;
    public const MTLPixelFormat kGBufferBFormat = MTLPixelFormat.RGBA8Snorm;
    public const MTLPixelFormat kDepthFormat = MTLPixelFormat.R32Float;

    private MTLDevice _device;

    private readonly List<IPass> _renderPasses = [];
    private readonly Dictionary<string, BufferVisualizationEntry> _bufferVisualizationEntries = new();
    private readonly List<(string Key, string Label)> _bufferVisualizationOptions = [];
    private string _selectedBufferVisualizationKey = string.Empty;

    private readonly struct BufferVisualizationEntry
    {
        public BufferVisualizationEntry(Func<MTLTexture> resolveTexture, BufferVisualizationChannels channels)
        {
            ResolveTexture = resolveTexture;
            Channels = channels;
        }

        public Func<MTLTexture> ResolveTexture { get; }
        public BufferVisualizationChannels Channels { get; }
    }

    /// <summary>
    /// GBuffer Texture containing BaseColor + Roughness
    /// </summary>
    [BufferVisualization("BaseColor")]
    [BufferVisualization("Roughness", BufferVisualizationChannels.A)]
    public MTLTexture GBufferA { get; private set; }

    /// <summary>
    /// GBuffer Texture containing WorldSpace Normals + Metalness
    /// </summary>
    [BufferVisualization("Normals")]
    [BufferVisualization("Metalness", BufferVisualizationChannels.A)]
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
    private readonly LightingPass _lightingPass;
    private readonly SkyboxPass _skyboxPass;
    private readonly DebugPresentPass _debugPresentPass;
    private readonly ShadowPass _shadowPass;
    private readonly Vector3 _mainLightDirection = Vector3.Normalize(new Vector3(-0.35f, 0.92f, 0.18f));

    private MTLBuffer[] _frameData = new MTLBuffer[kMaxFramesInFlight];
    private MTL4CommandBuffer[] _commandBuffers = new MTL4CommandBuffer[kMaxFramesInFlight];
    private MTL4CommandAllocator[] _commandAllocators = new MTL4CommandAllocator[kMaxFramesInFlight];

    private MTL4CommandQueue _queue;
    private MTLResidencySet _residencySet;

    private readonly Camera _camera;
    private readonly Scene _scene;

    public MTLTexture FinalTexture => HasActiveBufferVisualization ? _debugPresentPass.OutputTexture : _lightingPass.OutputTexture;

    public IReadOnlyList<(string Key, string Label)> BufferVisualizationOptions => _bufferVisualizationOptions;

    public RenderingPipeline(MTLDevice device, Scene scene, MTLTexture skyboxTexture, Camera camera, ulong initialWidth, ulong initialHeight)
    {
        _device = device;
        _camera = camera;
        _scene = scene;

        _queue = device.NewMTL4CommandQueue();
        var residencySetDescriptor = new MTLResidencySetDescriptor
        {
            InitialCapacity = 4096,
        };
        NSError residencySetError = default;
        _residencySet = _device.NewResidencySet(residencySetDescriptor, ref residencySetError);
        if (residencySetError.NativePtr != nint.Zero)
        {
            throw new Exception($"Failed to create residency set. Reason: {StringHelper.String(residencySetError.LocalizedDescription)}");
        }
        _queue.AddResidencySet(_residencySet);

        CreateGBuffer(initialWidth, initialHeight);

        _geometryPass = new GeometryPass(_device, this, scene, camera);
        _shadowPass = new ShadowPass(_device, this, scene);
        _lightingPass = new LightingPass(_device, this, _shadowPass);
        _skyboxPass = new SkyboxPass(_device, this, skyboxTexture, _lightingPass);
        _debugPresentPass = new DebugPresentPass(_device, this);
        ConfigureBufferVisualizationOptions();

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<FrameData>(), MTLResourceOptions.ResourceStorageModeShared);
            _commandBuffers[i] = device.NewCommandBuffer;
            _commandBuffers[i].Label = StringHelper.NSString("Frame Command Buffer");
            _commandAllocators[i] = device.NewCommandAllocator();
            TrackAllocation(_frameData[i].NativePtr);
        }

        RegisterSceneResidency(scene);
        _geometryPass.AddResidencyAllocations(_residencySet);
        _debugPresentPass.AddResidencyAllocations(_residencySet);
        _lightingPass.AddResidencyAllocations(_residencySet);
        _skyboxPass.AddResidencyAllocations(_residencySet);
        _shadowPass.AddResidencyAllocations(_residencySet);
        RegisterPipelineTextureResidency();
        _residencySet.Commit();
    }

    public void Render(in FrameInput frameInput, CAMetalDrawable currentDrawable, ImGuiRenderPass imGuiRenderPass)
    {
        if (currentDrawable.Texture.Width != GBufferA.Width || currentDrawable.Texture.Height != GBufferA.Height)
        {
            Resize(currentDrawable.Texture.Width , currentDrawable.Texture.Height);
        }

        Frame = (Frame + 1) % kMaxFramesInFlight;
        TrackAllocation(currentDrawable.Texture.NativePtr);
        _residencySet.Commit();

        var frameDataBuffer = _frameData[Frame];
        var commandBuffer = _commandBuffers[Frame];
        var commandAllocator = _commandAllocators[Frame];
        commandAllocator.Reset();
        commandBuffer.BeginCommandBuffer(commandAllocator);

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
            pFrameData->ViewProjectionMatrix = _camera.WorldToCameraMatrix * _camera.ProjectionMatrix;
            if (Matrix4x4.Invert(pFrameData->ViewProjectionMatrix, out Matrix4x4 invViewProjectionMatrix))
            {
                pFrameData->InverseViewProjectionMatrix = invViewProjectionMatrix;
            }
            pFrameData->ViewRotationMatrix = _camera.WorldToCameraMatrix;
            pFrameData->ViewRotationMatrix.Translation = Vector3.Zero;
            BuildDirectionalLightMatrices(_mainLightDirection, out Matrix4x4 lightViewMatrix, out Matrix4x4 lightProjectionMatrix);
            pFrameData->LightViewMatrix = lightViewMatrix;
            pFrameData->LightProjectionMatrix = lightProjectionMatrix;
            pFrameData->LightViewProjectionMatrix = lightViewMatrix * lightProjectionMatrix;
        }

        _renderPasses.Clear();
        _renderPasses.Add(_geometryPass);

        _lightingPass.LightDirection = _mainLightDirection;

        _renderPasses.Add(_shadowPass);
        _renderPasses.Add(_lightingPass);
        _renderPasses.Add(_skyboxPass);
        if (HasActiveBufferVisualization)
        {
            _renderPasses.Add(_debugPresentPass);
        }

        for (int passIndex = 0; passIndex < _renderPasses.Count; passIndex++)
        {
            var pass = _renderPasses[passIndex];
            pass.Execute(commandBuffer);

            if (passIndex < _renderPasses.Count - 1)
            {
                var barrierEncoder = commandBuffer.ComputeCommandEncoder;
                barrierEncoder.Label = StringHelper.NSString("Temporary Pass Barrier");
                barrierEncoder.BarrierAfterQueueStages(MTLStages.StageAll, MTLStages.StageAll, MTL4VisibilityOptions.Device);
                barrierEncoder.EndEncoding();
            }
        }

        var blitEncoder = commandBuffer.ComputeCommandEncoder;
        blitEncoder.BarrierAfterQueueStages(MTLStages.StageFragment, MTLStages.StageBlit, MTL4VisibilityOptions.Device);
        blitEncoder.Label = StringHelper.NSString("Copy From Texture");
        blitEncoder.CopyFromTexture(FinalTexture, currentDrawable.Texture);
        blitEncoder.EndEncoding();

        imGuiRenderPass.Render(frameInput, commandBuffer, currentDrawable.Texture, _residencySet, Frame);
        _residencySet.Commit();

        commandBuffer.EndCommandBuffer();
        _queue.Wait(currentDrawable);
        _queue.Commit([commandBuffer], 1);
        _queue.SignalDrawable(currentDrawable);

        currentDrawable.Present();
    }

    public void SetBufferVisualizationSelection(string key)
    {
        _selectedBufferVisualizationKey = key ?? string.Empty;
    }

    public bool HasActiveBufferVisualization => TryResolveBufferVisualization(_selectedBufferVisualizationKey, out _, out _);

    public bool TryGetActiveBufferVisualization(out MTLTexture texture, out BufferVisualizationChannels channels)
    {
        return TryResolveBufferVisualization(_selectedBufferVisualizationKey, out texture, out channels);
    }

    public MTLTexture ResolveBufferVisualizationTexture(string key)
    {
        if (TryResolveBufferVisualization(key, out MTLTexture texture, out _))
        {
            return texture;
        }

        return _lightingPass.OutputTexture;
    }

    private bool TryResolveBufferVisualization(string key, out MTLTexture texture, out BufferVisualizationChannels channels)
    {
        if (!string.IsNullOrWhiteSpace(key) && _bufferVisualizationEntries.TryGetValue(key, out BufferVisualizationEntry entry))
        {
            MTLTexture resolvedTexture = entry.ResolveTexture();
            if (resolvedTexture.NativePtr != nint.Zero)
            {
                texture = resolvedTexture;
                channels = entry.Channels;
                return true;
            }
        }

        texture = default;
        channels = BufferVisualizationChannels.RGB;
        return false;
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

    public void Resize(ulong width, ulong height)
    {
        if (width == 0 || height == 0)
        {
            return;
        }

        _camera.AspectRatio = width / (float)height;

        if (GBufferA.Width == width && GBufferA.Height == height)
        {
            return;
        }

        CreateGBuffer(width, height);
        _lightingPass.Resize(width, height);
        _debugPresentPass.Resize(width, height);
        RegisterPipelineTextureResidency();
        _residencySet.Commit();
    }

    private void RegisterSceneResidency(Scene scene)
    {
        foreach (var mesh in scene.Meshes)
        {
            TrackAllocation(mesh._vertexPositionsBuffer.NativePtr);
            TrackAllocation(mesh._vertexNormalsBuffer.NativePtr);
            TrackAllocation(mesh._vertexTangentsBuffer.NativePtr);
            TrackAllocation(mesh._vertexBitangentsBuffer.NativePtr);
            TrackAllocation(mesh._vertexTextureCoordinatesBuffer.NativePtr);
            TrackAllocation(mesh._indexBuffer.NativePtr);
            TrackAllocation(mesh.Material.Albedo.NativePtr);
            TrackAllocation(mesh.Material.Normals.NativePtr);
            TrackAllocation(mesh.Material.Opacity.NativePtr);
            TrackAllocation(mesh.Material.Roughness.NativePtr);
            TrackAllocation(mesh.Material.Metalness.NativePtr);
        }
    }

    private void RegisterPipelineTextureResidency()
    {
        TrackAllocation(GBufferA.NativePtr);
        TrackAllocation(GBufferB.NativePtr);
        TrackAllocation(GBufferDepth.NativePtr);
        TrackAllocation(Depth.NativePtr);
        TrackAllocation(_lightingPass.OutputTexture.NativePtr);
        TrackAllocation(_debugPresentPass.OutputTexture.NativePtr);
        TrackAllocation(_shadowPass.ShadowMap.NativePtr);
    }

    private void TrackAllocation(nint allocationPtr)
    {
        if (allocationPtr == nint.Zero)
        {
            return;
        }

        _residencySet.AddAllocation(new MTLAllocation(allocationPtr));
    }

    private void BuildDirectionalLightMatrices(Vector3 lightDirection, out Matrix4x4 lightView, out Matrix4x4 lightProjection)
    {
        Vector3 normalizedDirection = Vector3.Normalize(lightDirection);
        float sceneRadius = MathF.Max(_scene.SceneBounds.Extents.Length(), 1.0f);

        float xyPadding = MathF.Max(8.0f, sceneRadius * 0.15f);
        float zPadding = MathF.Max(16.0f, sceneRadius * 0.25f);
        float distanceFromCenter = sceneRadius + zPadding;

        Vector3 lightPos = _scene.SceneBounds.Center + normalizedDirection * distanceFromCenter;
        Vector3 up = MathF.Abs(Vector3.Dot(Vector3.UnitY, normalizedDirection)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        lightView = Matrix4x4.CreateLookAtLeftHanded(lightPos, _scene.SceneBounds.Center, up);

        // Conservative sphere fit around scene bounds to avoid matrix-convention errors while guaranteeing coverage.
        float orthoSize = (sceneRadius + xyPadding) * 2.0f;
        float nearPlane = MathF.Max(0.1f, distanceFromCenter - sceneRadius - zPadding);
        float farPlane = distanceFromCenter + sceneRadius + zPadding;
        lightProjection = Matrix4x4.CreateOrthographicLeftHanded(orthoSize, orthoSize, nearPlane, MathF.Max(farPlane, nearPlane + 1.0f));
    }

    private void ConfigureBufferVisualizationOptions()
    {
        _bufferVisualizationEntries.Clear();
        _bufferVisualizationOptions.Clear();

        RegisterBufferVisualizations("Pipeline", this);
        RegisterBufferVisualizations(nameof(GeometryPass), _geometryPass);
        RegisterBufferVisualizations(nameof(ShadowPass), _shadowPass);
        RegisterBufferVisualizations(nameof(LightingPass), _lightingPass);
        RegisterBufferVisualizations(nameof(SkyboxPass), _skyboxPass);
        _bufferVisualizationOptions.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));
    }

    private void RegisterBufferVisualizations(string ownerName, object owner)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type ownerType = owner.GetType();

        foreach (PropertyInfo property in ownerType.GetProperties(flags))
        {
            var visualizationAttributes = property.GetCustomAttributes<BufferVisualizationAttribute>().ToArray();
            foreach (var visualizationAttribute in visualizationAttributes)
            {
                if (property.PropertyType != typeof(MTLTexture))
                {
                    continue;
                }

                MethodInfo? getter = property.GetGetMethod(nonPublic: true);
                if (getter is null)
                {
                    continue;
                }

                string label = string.IsNullOrWhiteSpace(visualizationAttribute.DisplayName) ? property.Name : visualizationAttribute.DisplayName;
                string key = $"{ownerName}:property:{property.Name}:{visualizationAttribute.Channels}:{label}";
                _bufferVisualizationEntries[key] = new BufferVisualizationEntry(() => (MTLTexture)getter.Invoke(owner, null)!, visualizationAttribute.Channels);
                _bufferVisualizationOptions.Add((key, label));
            }
        }

        foreach (FieldInfo field in ownerType.GetFields(flags))
        {
            var visualizationAttributes = field.GetCustomAttributes<BufferVisualizationAttribute>().ToArray();
            foreach (var visualizationAttribute in visualizationAttributes)
            {
                if (field.FieldType != typeof(MTLTexture))
                {
                    continue;
                }

                string label = string.IsNullOrWhiteSpace(visualizationAttribute.DisplayName) ? field.Name : visualizationAttribute.DisplayName;
                string key = $"{ownerName}:field:{field.Name}:{visualizationAttribute.Channels}:{label}";
                _bufferVisualizationEntries[key] = new BufferVisualizationEntry(() => (MTLTexture)field.GetValue(owner)!, visualizationAttribute.Channels);
                _bufferVisualizationOptions.Add((key, label));
            }
        }
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
        public Matrix4x4 LightViewMatrix;
        public Matrix4x4 LightProjectionMatrix;
        public Matrix4x4 LightViewProjectionMatrix;
    }
}
