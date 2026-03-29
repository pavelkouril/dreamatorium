using Assimp;
using Dreamatorium.Assets;
using SharpMetal.Metal;
using Material = Dreamatorium.Rendering.Resources.Material;
using Mesh = Dreamatorium.Rendering.Resources.Mesh;

namespace Dreamatorium;

public class SponzaLoader
{
    private const float kSceneImportScale = 0.1f;

    public Rendering.Scene LoadFromFile(AssetLoader loader, string filePath, MTLDevice device)
    {
        var rv = new List<Mesh>();

        if (!File.Exists(filePath))
        {
            return new Rendering.Scene(rv);
        }

        using AssimpContext importer = new AssimpContext();
        importer.Scale = kSceneImportScale;

        Assimp.Scene scene = importer.ImportFile(filePath, PostProcessSteps.CalculateTangentSpace | PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.ImproveCacheLocality | PostProcessSteps.MakeLeftHanded | PostProcessSteps.FlipWindingOrder | PostProcessSteps.FlipUVs | PostProcessSteps.GlobalScale | PostProcessSteps.PreTransformVertices | PostProcessSteps.GenerateBoundingBoxes);

        if (scene == null)
        {
            return new Rendering.Scene(rv);
        }

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
                Albedo = AssignTextureOrFallback(loader, directory, aiMaterial, TextureType.Diffuse, loader.BlackDummy),
                Opacity = AssignTextureOrFallback(loader, directory, aiMaterial, TextureType.Opacity, loader.WhiteDummy),
                Normals = AssignTextureOrFallback(loader, directory, aiMaterial, TextureType.Height, loader.BlackDummy),
                Roughness = AssignTextureOrFallback(loader, directory, aiMaterial, TextureType.Shininess, loader.BlackDummy),
                Metalness = AssignTextureOrFallback(loader, directory, aiMaterial, TextureType.Ambient, loader.BlackDummy)
            };
        }

        foreach (var mesh in scene.Meshes)
        {
            var m = Mesh.FromAssimpMesh(mesh, materials[mesh.MaterialIndex], device);
            rv.Add(m);
        }

        return new Rendering.Scene(rv);
    }

    private MTLTexture AssignTextureOrFallback(AssetLoader loader, string directory, Assimp.Material aiMaterial, TextureType type, MTLTexture fallbackTexture)
    {
        if (!aiMaterial.GetMaterialTexture(type, 0, out var textureSlot))
        {
            return fallbackTexture;
        }

        string texturePath = textureSlot.FilePath;
        var fullPath = Path.Join(directory, texturePath);

        return loader.LoadTexture(fullPath, type);
    }
}
