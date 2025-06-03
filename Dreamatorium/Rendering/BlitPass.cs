using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class BlitPass(MTLCommandQueue queue) : IPass
{
    private MTLCommandQueue _queue = queue;

    public MTLTexture Destination;

    public MTLTexture Source;

    public void Execute()
    {
        var commandBuffer = _queue.CommandBuffer();
        commandBuffer.Label = StringHelper.NSString("Blit/CommandBuffer");

        var desc = new MTLBlitPassDescriptor();
        var blitEncoder = commandBuffer.BlitCommandEncoder(desc);
        blitEncoder.Label = StringHelper.NSString("Blit/Encoder");
        blitEncoder.CopyFromTexture(Source, Destination);
        blitEncoder.EndEncoding();

        commandBuffer.Commit();
    }
}
