namespace Dreamatorium.Rendering;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class BufferVisualizationAttribute : Attribute
{
    public string? DisplayName { get; }

    public BufferVisualizationAttribute()
    {
    }

    public BufferVisualizationAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}
