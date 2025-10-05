using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public class ShadowPass : IPass
{
    private MTLTexture _shadowTexture;

    public ShadowPass(MTLDevice device)
    {
        device.NewTexture(new MTLTextureDescriptor()
        {
            PixelFormat = MTLPixelFormat.Depth32Float,
        });
        _shadowTexture = new MTLTexture();
    }

    public void Execute()
    {

    }
}
