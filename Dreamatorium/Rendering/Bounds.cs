using System.Numerics;
using System.Runtime.InteropServices;

namespace Dreamatorium.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct Bounds
{
    public Vector3 Min;
    public Vector3 Max;

    public Vector3 Extents => (Max - Min) * 0.5f;
    public Vector3 Center => (Min + Max) * 0.5f;

    public Bounds(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }
}
