using System.Numerics;

namespace Dreamatorium.Utils;

public static class MathExtensions
{
    public static float Deg2Rad(float degrees) => degrees * MathF.PI / 180.0f;

    public static float Rad2Deg(float radians) => radians * 180.0f / MathF.PI;

    public static Vector3 RotateVector(this Quaternion rotation, Vector3 vector)
    {
        var r = new Vector3(rotation.X, rotation.Y, rotation.Z);
        float w = rotation.W;

        return 2 * Vector3.Dot(r, vector) * r + (w*w - Vector3.Dot(r, r)) * vector + 2 * w * Vector3.Cross(r, vector);
    }
}
