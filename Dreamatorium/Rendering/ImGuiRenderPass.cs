using Dreamatorium.Platforms.macOS;
using Dreamatorium.UI;
using Dreamatorium.Input;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public sealed class ImGuiRenderPass
{
    private readonly ImGuiController _imGuiController;
    private readonly AppUi _ui;

    public ImGuiRenderPass(MTLDevice device, AppUi ui)
    {
        _imGuiController = new ImGuiController(device, MTLPixelFormat.RGBA8Unorm);
        _ui = ui;
    }

    public InputCaptureState BeginFrame(in FrameInput frameInput, MTKView view, MTLTexture destinationTexture)
    {
        return _imGuiController.BeginFrame(frameInput, destinationTexture.Width, destinationTexture.Height, view.BackingScaleFactor);
    }

    public void Render(in FrameInput frameInput, MTL4CommandBuffer commandBuffer, MTLTexture destinationTexture, MTLResidencySet residencySet, int frameIndex)
    {
        _ui.Draw(frameInput);
        _imGuiController.Render(commandBuffer, destinationTexture, residencySet, frameIndex);
    }
}
