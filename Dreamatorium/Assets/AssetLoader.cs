using System.Buffers;
using Assimp;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamatorium.Assets;

public unsafe class AssetLoader
{
    private MTLDevice _device;
    private MTLTexture _whiteDummy;
    private MTLTexture _blackDummy;

    private readonly Dictionary<string, MTLTexture> _textureLibrary = new();

    public MTLTexture WhiteDummy => _whiteDummy;

    public MTLTexture BlackDummy => _blackDummy;

    public AssetLoader(MTLDevice device)
    {
        _device = device;
        CreateBlanks();
    }

    public MTLTexture LoadTexture(string fullPath, TextureType type)
    {
        if (!_textureLibrary.TryGetValue(fullPath, out var texture))
        {
            using var image = Image.Load<Rgba32>(fullPath);
            if (!ToMTLTexture(fullPath, image, type, out texture))
            {
                Console.Error.WriteLine($"Failed to load texture {fullPath}");
            }

            _textureLibrary[fullPath] = texture;
        }

        return texture;
    }

    private void CreateBlanks()
    {
        using Image<Rgba32> whiteImage = new Image<Rgba32>(1, 1);
        whiteImage[0, 0] = Color.White;
        if (!ToMTLTexture("DummyWhite", whiteImage, TextureType.None, out _whiteDummy))
        {
            Console.Error.WriteLine("Can't create DummyWhite");
        }

        using Image<Rgba32> blackImage = new Image<Rgba32>(1, 1);
        blackImage[0, 0] = Color.Black;
        if (!ToMTLTexture("DummyWhite", blackImage, TextureType.None, out _blackDummy))
        {
            Console.Error.WriteLine("Can't create DummyBlack");
        }
    }

    private bool ToMTLTexture(string fullPath, Image<Rgba32> image, TextureType type, out MTLTexture texture)
    {
        if (!image.DangerousTryGetSinglePixelMemory(out var memory))
        {
            texture = default;
            return false;
        }

        var textureDescriptor = new MTLTextureDescriptor()
        {
            Width = (ulong)image.Width,
            Height = (ulong)image.Height,
            PixelFormat = type == TextureType.Diffuse ? MTLPixelFormat.RGBA8UnormsRGB : MTLPixelFormat.RGBA8Unorm,
        };

        texture = _device.NewTexture(textureDescriptor);
        var region = new MTLRegion()
        {
            origin = new MTLOrigin() { x = 0, y = 0, z = 0 },
            size = new MTLSize() { width = textureDescriptor.Width, height = textureDescriptor.Height, depth = 1 },
        };
        ulong bytesPerRow = 4 * textureDescriptor.Width;

        texture.Label = StringHelper.NSString(Path.GetFileNameWithoutExtension(fullPath));

        using MemoryHandle pinHandle = memory.Pin();
        texture.ReplaceRegion(region, 0, new nint(pinHandle.Pointer), bytesPerRow);

        return true;
    }
}