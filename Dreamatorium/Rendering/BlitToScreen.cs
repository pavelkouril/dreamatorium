using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;

namespace Dreamatorium.Rendering;

public class BlitToScreen : IPass
{
    private MTLCommandQueue _queue;

    public CAMetalDrawable Drawable;

    public MTLTexture TextureToPresent;

    public BlitToScreen(MTLDevice device)
    {
        _queue = device.NewCommandQueue();
    }

    public void Execute()
    {
        var commandBuffer = _queue.CommandBuffer();
        commandBuffer.Label = StringHelper.NSString("Blit/CommandBuffer");

        var desc = new MTLBlitPassDescriptor();
        var blitEncoder = commandBuffer.BlitCommandEncoder(desc);
        blitEncoder.Label = StringHelper.NSString("Blit/Encoder");

        blitEncoder.CopyFromTexture(TextureToPresent, Drawable.Texture);
        blitEncoder.EndEncoding();

        commandBuffer.PresentDrawable(Drawable);
        commandBuffer.Commit();
    }
}
