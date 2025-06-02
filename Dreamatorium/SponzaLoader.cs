using System.Buffers;
using Assimp;
using Dreamatorium.Platforms.macOS;
using SharpMetal.Metal;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Material = Dreamatorium.Rendering.Resources.Material;
using Mesh = Dreamatorium.Rendering.Resources.Mesh;

namespace Dreamatorium;

public unsafe class SponzaLoader
{
    private readonly Dictionary<string, MTLTexture> _textureLibrary = new();

    private MTLTexture _whiteDummy;
    private MTLTexture _blackDummy;

    public List<Mesh> LoadFromFile(string filePath, MTLDevice device)
    {
        var rv = new List<Mesh>();

        if (!File.Exists(filePath))
        {
            return rv;
        }

        AssimpContext importer = new AssimpContext();

        Assimp.Scene scene = importer.ImportFile(filePath, PostProcessSteps.CalculateTangentSpace | PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.ImproveCacheLocality | PostProcessSteps.FlipUVs);

        if (scene == null)
        {
            return rv;
        }

        CreateBlanks(device);

        var materials = new Material[scene.MaterialCount];

        var directory = Path.GetDirectoryName(filePath);
        if (directory == null)
        {
            throw new Exception($"Failed to load {filePath}, cannot resolve directory.");
        }

        for (int i = 0; i < scene.MaterialCount; i++)
        {
            var aiMaterial = scene.Materials[i];

            materials[i] = new Material(aiMaterial.Name, i)
            {
                Albedo = AssignTextureOrFallback(device, directory, aiMaterial, TextureType.Diffuse, _blackDummy),
                Opacity = AssignTextureOrFallback(device, directory, aiMaterial, TextureType.Opacity, _whiteDummy),
                Normals = AssignTextureOrFallback(device, directory, aiMaterial, TextureType.Height, _blackDummy),
                Roughness = AssignTextureOrFallback(device, directory, aiMaterial, TextureType.Shininess, _blackDummy),
                Metalness = AssignTextureOrFallback(device, directory, aiMaterial, TextureType.Ambient, _blackDummy)
            };
        }

        foreach (var mesh in scene.Meshes)
        {
            var m = Mesh.FromAssimpMesh(mesh, materials[mesh.MaterialIndex], device);
            rv.Add(m);
        }

        return rv;
    }

    private MTLTexture AssignTextureOrFallback(MTLDevice device, string directory, Assimp.Material aiMaterial, TextureType type, MTLTexture fallbackTexture)
    {
        if (!aiMaterial.GetMaterialTexture(type, 0, out var textureSlot))
        {
            return fallbackTexture;
        }

        string texturePath = textureSlot.FilePath;

        if (!_textureLibrary.TryGetValue(texturePath, out var texture))
        {
            var fullPath = Path.Join(directory, textureSlot.FilePath);
            if (!LoadTexture(device, fullPath, out texture))
            {
                Console.Error.WriteLine($"Failed to load texture {fullPath}");
            }

            _textureLibrary[texturePath] = texture;
        }

        return texture;
    }

    private void CreateBlanks(MTLDevice device)
    {
        using Image<Rgba32> whiteImage = new Image<Rgba32>(1, 1);
        whiteImage[0, 0] = Color.White;
        if (!ToMTLTexture(device, "DummyWhite", whiteImage, out _whiteDummy))
        {
            Console.Error.WriteLine("Can't create DummyWhite");
        }

        using Image<Rgba32> blackImage = new Image<Rgba32>(1, 1);
        blackImage[0, 0] = Color.Black;
        if (!ToMTLTexture(device, "DummyWhite", blackImage, out _blackDummy))
        {
            Console.Error.WriteLine("Can't create DummyBlack");
        }
    }

    private bool LoadTexture(MTLDevice device, string fullPath, out MTLTexture texture)
    {
        using var image = Image.Load<Rgba32>(fullPath);
        return ToMTLTexture(device, fullPath, image, out texture);
    }

    private static bool ToMTLTexture(MTLDevice device, string fullPath, Image<Rgba32> image, out MTLTexture texture)
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
        };

        texture = device.NewTexture(textureDescriptor);
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
