namespace Dreamatorium.Platforms;

/// <summary>
/// This solely serves as quick & simple way to prevent GC collection of the delegates and other objects that are passed as function pointers into native calls.
/// </summary>
public static class InteropKeepAliveRegistry
{
    private static List<object> _delegates = new();

    public static void Add(object @delegate)
    {
        _delegates.Add(@delegate);
    }
}
