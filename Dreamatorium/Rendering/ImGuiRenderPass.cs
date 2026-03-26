using Dreamatorium.Platforms.macOS;
using Dreamatorium.UI;
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

    public void Execute(in FrameInput frameInput, MTKView view, MTLCommandBuffer commandBuffer, MTLTexture destinationTexture)
    {
        _imGuiController.BeginFrame(frameInput, destinationTexture.Width, destinationTexture.Height, view.BackingScaleFactor);
        _ui.Draw(frameInput);
        _imGuiController.Render(commandBuffer, destinationTexture);
    }
}
