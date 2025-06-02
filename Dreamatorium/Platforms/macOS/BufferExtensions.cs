using SharpMetal.Metal;

namespace Dreamatorium.Platforms.macOS;

public static unsafe class BufferExtensions
{
    public static void CopyToBuffer<T>(this T[] source, MTLBuffer buffer)
    {
        var span = new Span<T>(buffer.Contents.ToPointer(), source.Length);
        source.CopyTo(span);
    }

    public static void CopyToBuffer<T>(this List<T> source, MTLBuffer buffer)
    {
        var span = new Span<T>(buffer.Contents.ToPointer(), source.Count);
        source.CopyTo(span);
    }
}
