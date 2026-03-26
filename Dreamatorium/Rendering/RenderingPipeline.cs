using System.Numerics;
using System.Runtime.InteropServices;
using System.Reflection;
using Dreamatorium.Input;
using Dreamatorium.Rendering.Resources;
using Dreamatorium.Scene;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class RenderingPipeline
{
    public const int kMaxFramesInFlight = 3;

    public const MTLPixelFormat kGBufferAFormat = MTLPixelFormat.RGBA8Unorm;
    public const MTLPixelFormat kGBufferBFormat = MTLPixelFormat.RGBA8Snorm;
    public const MTLPixelFormat kDepthFormat = MTLPixelFormat.R32Float;

    private MTLDevice _device;

    private readonly List<IPass> _renderPasses = [];
    private readonly Dictionary<string, Func<MTLTexture>> _bufferVisualizationResolvers = new();
    private readonly List<(string Key, string Label)> _bufferVisualizationOptions = [];

    /// <summary>
    /// GBuffer Texture containing BaseColor + Roughness
    /// </summary>
    [BufferVisualization]
    public MTLTexture GBufferA { get; private set; }

    /// <summary>
    /// GBuffer Texture containing WorldSpace Normals + Metalness
    /// </summary>
    [BufferVisualization]
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
    private readonly ShadowPass _shadowPass;
    private readonly Vector3 _mainLightDirection = Vector3.Normalize(new Vector3(-1, 1, 1));

    private MTLBuffer[] _frameData = new MTLBuffer[kMaxFramesInFlight];

    private MTLCommandQueue _queue;

    private readonly Camera _camera;

    public MTLTexture FinalTexture => _lightingPass.OutputTexture;

    public IReadOnlyList<(string Key, string Label)> BufferVisualizationOptions => _bufferVisualizationOptions;

    public RenderingPipeline(MTLDevice device, List<Mesh> scene, MTLTexture skyboxTexture, Camera camera, ulong initialWidth, ulong initialHeight)
    {
        _device = device;
        _camera = camera;

        _queue = device.NewCommandQueue();

        CreateGBuffer(initialWidth, initialHeight);

        _geometryPass = new GeometryPass(_device, _queue, this, scene, camera);
        _shadowPass = new ShadowPass(_device, _queue, this, scene);
        _lightingPass = new LightingPass(_device, _queue, this, _shadowPass);
        _skyboxPass = new SkyboxPass(_device, _queue, this, skyboxTexture, _lightingPass);
        ConfigureBufferVisualizationOptions();

        for (int i = 0; i < _frameData.Length; i++)
        {
            _frameData[i] = device.NewBuffer((ulong)Marshal.SizeOf<FrameData>(), MTLResourceOptions.ResourceStorageModeShared);
        }
    }

    public void Render(in FrameInput frameInput, ulong targetWidth, ulong targetHeight)
    {
        if (targetWidth != GBufferA.Width || targetHeight != GBufferA.Height)
        {
            Resize(targetWidth, targetHeight);
        }

        Frame = (Frame + 1) % kMaxFramesInFlight;
        var frameDataBuffer = _frameData[Frame];
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
            pFrameData->ViewProjectionMatrix = _camera.ProjectionMatrix * _camera.WorldToCameraMatrix;
            if (Matrix4x4.Invert(pFrameData->ViewProjectionMatrix, out Matrix4x4 invViewProjectionMatrix))
            {
                pFrameData->InverseViewProjectionMatrix = invViewProjectionMatrix;
            }
            pFrameData->ViewRotationMatrix = _camera.WorldToCameraMatrix;
            pFrameData->ViewRotationMatrix.Translation = Vector3.Zero;
            BuildDirectionalLightMatrices(_mainLightDirection, out Matrix4x4 lightViewMatrix, out Matrix4x4 lightProjectionMatrix);
            pFrameData->LightViewMatrix = lightViewMatrix;
            pFrameData->LightProjectionMatrix = lightProjectionMatrix;
            pFrameData->LightViewProjectionMatrix = lightProjectionMatrix * lightViewMatrix;
        }

        _renderPasses.Clear();
        _renderPasses.Add(_geometryPass);

        _lightingPass.LightDirection = _mainLightDirection;

        _renderPasses.Add(_shadowPass);
        _renderPasses.Add(_lightingPass);

        _renderPasses.Add(_skyboxPass);

        foreach (var pass in _renderPasses)
        {
            pass.Execute();
        }
    }

    public MTLCommandBuffer CreateFrameCommandBuffer(string label)
    {
        var commandBuffer = _queue.CommandBuffer();
        commandBuffer.Label = StringHelper.NSString(label);
        return commandBuffer;
    }

    public MTLTexture ResolveBufferVisualizationTexture(string key)
    {
        if (_bufferVisualizationResolvers.TryGetValue(key, out Func<MTLTexture>? resolveTexture))
        {
            MTLTexture selectedTexture = resolveTexture();
            if (selectedTexture.NativePtr != nint.Zero)
            {
                return selectedTexture;
            }
        }

        return FinalTexture;
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
    }

    private static void BuildDirectionalLightMatrices(Vector3 lightDirection, out Matrix4x4 lightView, out Matrix4x4 lightProjection)
    {
        // World-anchored directional light setup to keep shadows stable when camera moves.
        float distanceFromCenter = 120.0f;
        float orthoWidth = 240.0f;
        float orthoHeight = 240.0f;
        float nearPlane = 1.0f;
        float farPlane = 600.0f;

        Vector3 target = Vector3.Zero;
        Vector3 lightPos = target + lightDirection * distanceFromCenter;
        Vector3 up = MathF.Abs(Vector3.Dot(Vector3.UnitY, lightDirection)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;

        lightView = Matrix4x4.CreateLookAtLeftHanded(lightPos, target, up);
        lightProjection = Matrix4x4.CreateOrthographicLeftHanded(orthoWidth, orthoHeight, nearPlane, farPlane);
    }

    private void ConfigureBufferVisualizationOptions()
    {
        _bufferVisualizationResolvers.Clear();
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
            BufferVisualizationAttribute? visualizationAttribute = property.GetCustomAttribute<BufferVisualizationAttribute>();
            if (property.PropertyType != typeof(MTLTexture) || visualizationAttribute is null)
            {
                continue;
            }

            MethodInfo? getter = property.GetGetMethod(nonPublic: true);
            if (getter is null)
            {
                continue;
            }

            string key = $"{ownerName}:property:{property.Name}";
            string label = string.IsNullOrWhiteSpace(visualizationAttribute.DisplayName) ? property.Name : visualizationAttribute.DisplayName;
            _bufferVisualizationResolvers[key] = () => (MTLTexture)getter.Invoke(owner, null)!;
            _bufferVisualizationOptions.Add((key, label));
        }

        foreach (FieldInfo field in ownerType.GetFields(flags))
        {
            BufferVisualizationAttribute? visualizationAttribute = field.GetCustomAttribute<BufferVisualizationAttribute>();
            if (field.FieldType != typeof(MTLTexture) || visualizationAttribute is null)
            {
                continue;
            }

            string key = $"{ownerName}:field:{field.Name}";
            string label = string.IsNullOrWhiteSpace(visualizationAttribute.DisplayName) ? field.Name : visualizationAttribute.DisplayName;
            _bufferVisualizationResolvers[key] = () => (MTLTexture)field.GetValue(owner)!;
            _bufferVisualizationOptions.Add((key, label));
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
