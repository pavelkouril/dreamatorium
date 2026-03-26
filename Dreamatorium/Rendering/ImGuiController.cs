using System.Numerics;
using ImGuiNET;
using Dreamatorium.Input;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public sealed class ImGuiController
{
    private MTLDevice _device;
    private readonly MTLRenderPassDescriptor _renderPassDescriptor = new MTLRenderPassDescriptor();
    private readonly MTLRenderPipelineState _pipelineState;
    private readonly MTLSamplerState _samplerState;
    private readonly MTLBuffer _projectionBuffer;
    private readonly MTLTexture _fontTexture;

    private const int kImGuiTextureId = 1;
    private const int kMouseButtonCount = 3;

    public ImGuiController(MTLDevice device, MTLPixelFormat colorFormat)
    {
        _device = device;

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.Fonts.AddFontDefault();

        _pipelineState = CreatePipelineState(colorFormat);
        _samplerState = CreateSamplerState();
        _projectionBuffer = _device.NewBuffer((ulong)sizeof(float) * 16, MTLResourceOptions.ResourceStorageModeShared);
        _fontTexture = CreateFontTexture();
    }

    public InputCaptureState BeginFrame(in FrameInput frameInput, ulong width, ulong height, float framebufferScale)
    {
        var io = ImGui.GetIO();
        framebufferScale = MathF.Max(framebufferScale, 1.0f);
        float displayWidthPoints = width / framebufferScale;
        float displayHeightPoints = height / framebufferScale;
        io.DisplaySize = new Vector2(displayWidthPoints, displayHeightPoints);
        io.DisplayFramebufferScale = new Vector2(framebufferScale, framebufferScale);
        io.DeltaTime = MathF.Max(frameInput.DeltaTime, 1e-4f);

        UpdateInput(io, frameInput, displayHeightPoints);

        ImGui.NewFrame();

        return new InputCaptureState(io.WantCaptureMouse,io.WantCaptureKeyboard || io.WantTextInput);
    }

    public unsafe void Render(MTLCommandBuffer commandBuffer, MTLTexture destination)
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount <= 0 || drawData.TotalVtxCount <= 0)
        {
            return;
        }

        var colorAttachment = _renderPassDescriptor.ColorAttachments.Object(0);
        colorAttachment.Texture = destination;
        colorAttachment.LoadAction = MTLLoadAction.Load;
        colorAttachment.StoreAction = MTLStoreAction.Store;
        _renderPassDescriptor.ColorAttachments.SetObject(colorAttachment, 0);

        var encoder = commandBuffer.RenderCommandEncoder(_renderPassDescriptor);
        encoder.Label = StringHelper.NSString("ImGui.Encoder");
        encoder.SetRenderPipelineState(_pipelineState);
        encoder.SetViewport(new MTLViewport
        {
            originX = 0.0,
            originY = 0.0,
            width = destination.Width,
            height = destination.Height,
            znear = 0.0,
            zfar = 1.0
        });
        encoder.SetFragmentSamplerState(_samplerState, 0);
        SetProjectionMatrix(encoder, drawData);

        var clipOff = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;
        var indexType = MTLIndexType.UInt16;
        const int indexSizeInBytes = sizeof(ushort);

        for (int listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            var cmdList = drawData.CmdLists[listIndex];
            ulong vertexBytes = (ulong)(cmdList.VtxBuffer.Size * sizeof(ImDrawVert));
            ulong indexBytes = (ulong)(cmdList.IdxBuffer.Size * indexSizeInBytes);
            var vertexBuffer = _device.NewBuffer(vertexBytes, MTLResourceOptions.ResourceStorageModeShared);
            var indexBuffer = _device.NewBuffer(indexBytes, MTLResourceOptions.ResourceStorageModeShared);

            Buffer.MemoryCopy(cmdList.VtxBuffer.Data.ToPointer(), vertexBuffer.Contents.ToPointer(), vertexBytes, vertexBytes);
            Buffer.MemoryCopy(cmdList.IdxBuffer.Data.ToPointer(), indexBuffer.Contents.ToPointer(), indexBytes, indexBytes);

            for (int cmdIndex = 0; cmdIndex < cmdList.CmdBuffer.Size; cmdIndex++)
            {
                var cmd = cmdList.CmdBuffer[cmdIndex];
                if (cmd.UserCallback != IntPtr.Zero)
                {
                    continue;
                }

                Vector4 clipRect = new(
                    (cmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.Y - clipOff.Y) * clipScale.Y,
                    (cmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.W - clipOff.Y) * clipScale.Y);

                if (clipRect.X >= clipRect.Z || clipRect.Y >= clipRect.W)
                {
                    continue;
                }

                ulong scissorX = (ulong)Math.Max(0, (int)MathF.Floor(clipRect.X));
                ulong scissorY = (ulong)Math.Max(0, (int)MathF.Floor(clipRect.Y));
                ulong scissorWidth = (ulong)Math.Max(0, (int)MathF.Ceiling(clipRect.Z - clipRect.X));
                ulong scissorHeight = (ulong)Math.Max(0, (int)MathF.Ceiling(clipRect.W - clipRect.Y));

                if (scissorWidth == 0 || scissorHeight == 0)
                {
                    continue;
                }

                encoder.SetScissorRect(new MTLScissorRect
                {
                    x = scissorX,
                    y = scissorY,
                    width = scissorWidth,
                    height = scissorHeight
                });

                encoder.SetVertexBuffer(vertexBuffer, (ulong)(cmd.VtxOffset * sizeof(ImDrawVert)), 0);

                var drawTexture = _fontTexture;
                encoder.SetFragmentTexture(drawTexture, 0);
                encoder.DrawIndexedPrimitives(
                    MTLPrimitiveType.Triangle,
                    cmd.ElemCount,
                    indexType,
                    indexBuffer,
                    cmd.IdxOffset * indexSizeInBytes);
            }
        }

        encoder.EndEncoding();
    }

    private unsafe void SetProjectionMatrix(MTLRenderCommandEncoder encoder, ImDrawDataPtr drawData)
    {
        float left = drawData.DisplayPos.X;
        float right = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float top = drawData.DisplayPos.Y;
        float bottom = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        Matrix4x4 projection = new(
            2.0f / (right - left), 0, 0, 0,
            0, 2.0f / (top - bottom), 0, 0,
            0, 0, 0.5f, 0,
            (right + left) / (left - right), (top + bottom) / (bottom - top), 0.5f, 1.0f);

        *(Matrix4x4*)_projectionBuffer.Contents = projection;
        encoder.SetVertexBuffer(_projectionBuffer, 0, 1);
    }

    private static void UpdateInput(ImGuiIOPtr io, in FrameInput frameInput, float viewHeightPoints)
    {
        bool[] keyDown = frameInput.Input.KeyDownState;
        SetKey(io, ImGuiKey.A, keyDown, 0);
        SetKey(io, ImGuiKey.B, keyDown, 11);
        SetKey(io, ImGuiKey.C, keyDown, 8);
        SetKey(io, ImGuiKey.D, keyDown, 2);
        SetKey(io, ImGuiKey.E, keyDown, 14);
        SetKey(io, ImGuiKey.F, keyDown, 3);
        SetKey(io, ImGuiKey.G, keyDown, 5);
        SetKey(io, ImGuiKey.H, keyDown, 4);
        SetKey(io, ImGuiKey.I, keyDown, 34);
        SetKey(io, ImGuiKey.J, keyDown, 38);
        SetKey(io, ImGuiKey.K, keyDown, 40);
        SetKey(io, ImGuiKey.L, keyDown, 37);
        SetKey(io, ImGuiKey.M, keyDown, 46);
        SetKey(io, ImGuiKey.N, keyDown, 45);
        SetKey(io, ImGuiKey.O, keyDown, 31);
        SetKey(io, ImGuiKey.P, keyDown, 35);
        SetKey(io, ImGuiKey.Q, keyDown, 12);
        SetKey(io, ImGuiKey.R, keyDown, 15);
        SetKey(io, ImGuiKey.S, keyDown, 1);
        SetKey(io, ImGuiKey.T, keyDown, 17);
        SetKey(io, ImGuiKey.U, keyDown, 32);
        SetKey(io, ImGuiKey.V, keyDown, 9);
        SetKey(io, ImGuiKey.W, keyDown, 13);
        SetKey(io, ImGuiKey.X, keyDown, 7);
        SetKey(io, ImGuiKey.Y, keyDown, 16);
        SetKey(io, ImGuiKey.Z, keyDown, 6);

        SetKey(io, ImGuiKey.Enter, keyDown, 36);
        SetKey(io, ImGuiKey.Escape, keyDown, 53);
        SetKey(io, ImGuiKey.Tab, keyDown, 48);
        SetKey(io, ImGuiKey.Backspace, keyDown, 51);
        SetKey(io, ImGuiKey.Space, keyDown, 49);
        SetKey(io, ImGuiKey.LeftArrow, keyDown, 123);
        SetKey(io, ImGuiKey.RightArrow, keyDown, 124);
        SetKey(io, ImGuiKey.DownArrow, keyDown, 125);
        SetKey(io, ImGuiKey.UpArrow, keyDown, 126);

        bool shift = KeyState(keyDown, 56) || KeyState(keyDown, 60);
        bool ctrl = KeyState(keyDown, 59) || KeyState(keyDown, 62);
        bool alt = KeyState(keyDown, 58) || KeyState(keyDown, 61);
        bool super = KeyState(keyDown, 55) || KeyState(keyDown, 54);
        io.AddKeyEvent(ImGuiKey.ModShift, shift);
        io.AddKeyEvent(ImGuiKey.ModCtrl, ctrl);
        io.AddKeyEvent(ImGuiKey.ModAlt, alt);
        io.AddKeyEvent(ImGuiKey.ModSuper, super);

        io.AddMousePosEvent(frameInput.Input.MouseX, viewHeightPoints - frameInput.Input.MouseY);

        for (int buttonIndex = 0; buttonIndex < kMouseButtonCount; buttonIndex++)
        {
            io.AddMouseButtonEvent(buttonIndex, frameInput.Input.MouseButtonState[buttonIndex]);
        }

        io.AddMouseWheelEvent(frameInput.Input.MouseWheelX, frameInput.Input.MouseWheelY);
    }

    private static bool KeyState(bool[] keyDown, int keyCode)
    {
        return (uint)keyCode < keyDown.Length && keyDown[keyCode];
    }

    private static void SetKey(ImGuiIOPtr io, ImGuiKey key, bool[] keyDown, int keyCode)
    {
        io.AddKeyEvent(key, KeyState(keyDown, keyCode));
    }

    private MTLRenderPipelineState CreatePipelineState(MTLPixelFormat colorFormat)
    {
        var library = ShaderLibrary.GetOrCreate(_device);
        var vertex = library.NewFunction(StringHelper.NSString("imgui_vs"));
        var fragment = library.NewFunction(StringHelper.NSString("imgui_fs"));

        var descriptor = new MTLRenderPipelineDescriptor
        {
            VertexFunction = vertex,
            FragmentFunction = fragment,
            DepthAttachmentPixelFormat = MTLPixelFormat.Invalid,
            StencilAttachmentPixelFormat = MTLPixelFormat.Invalid
        };
        var color = descriptor.ColorAttachments.Object(0);
        color.PixelFormat = colorFormat;
        color.IsBlendingEnabled = true;
        color.SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
        color.SourceAlphaBlendFactor = MTLBlendFactor.SourceAlpha;
        color.DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
        color.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
        color.RgbBlendOperation = MTLBlendOperation.Add;
        color.AlphaBlendOperation = MTLBlendOperation.Add;

        NSError err = default;
        var state = _device.NewRenderPipelineState(descriptor, ref err);
        if (err.NativePtr != nint.Zero)
        {
            throw new InvalidOperationException($"Failed creating ImGui pipeline: {StringHelper.String(err.LocalizedDescription)}");
        }

        return state;
    }

    private MTLSamplerState CreateSamplerState()
    {
        var descriptor = new MTLSamplerDescriptor
        {
            MinFilter = MTLSamplerMinMagFilter.Linear,
            MagFilter = MTLSamplerMinMagFilter.Linear,
            SAddressMode = MTLSamplerAddressMode.ClampToEdge,
            TAddressMode = MTLSamplerAddressMode.ClampToEdge
        };
        return _device.NewSamplerState(descriptor);
    }

    private unsafe MTLTexture CreateFontTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out _);

        var descriptor = new MTLTextureDescriptor
        {
            Width = (ulong)width,
            Height = (ulong)height,
            MipmapLevelCount = 1,
            TextureType = MTLTextureType.Type2D,
            StorageMode = MTLStorageMode.Shared,
            Usage = MTLTextureUsage.ShaderRead,
            PixelFormat = MTLPixelFormat.RGBA8Unorm
        };

        var texture = _device.NewTexture(descriptor);
        texture.Label = StringHelper.NSString("ImGui.FontAtlas");
        texture.ReplaceRegion(new MTLRegion
        {
            origin = new MTLOrigin { x = 0, y = 0, z = 0 },
            size = new MTLSize { width = descriptor.Width, height = descriptor.Height, depth = 1 }
        }, 0, (nint)pixels, (ulong)(width * 4));

        io.Fonts.SetTexID(kImGuiTextureId);
        io.Fonts.ClearTexData();
        return texture;
    }
}
