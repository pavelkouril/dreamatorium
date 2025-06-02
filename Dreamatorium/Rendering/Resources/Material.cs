using SharpMetal.Metal;

namespace Dreamatorium.Rendering.Resources;

public class Material(string name, int index)
{
   public string Name { get; private set; } = name;
   public int Index { get; private set; } = index;

   public MTLTexture Metalness;
   public MTLTexture Albedo;
   public MTLTexture Opacity;
   public MTLTexture Normals;
   public MTLTexture Roughness;
}