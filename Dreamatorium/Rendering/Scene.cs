using Dreamatorium.Rendering.Resources;
using System.Numerics;

namespace Dreamatorium.Rendering;

public class Scene
{
    public List<Mesh> Meshes { get; private set; }

    public Bounds SceneBounds { get; private set; }

    public Scene(List<Mesh> meshes)
    {
        Meshes = meshes;

        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);

        foreach (var mesh in Meshes)
        {
            min = Vector3.Min(min, mesh.Bounds.Min);
            max = Vector3.Max(max, mesh.Bounds.Max);
        }

        SceneBounds = new Bounds(min, max);
    }
}
