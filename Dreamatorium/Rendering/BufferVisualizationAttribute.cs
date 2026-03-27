namespace Dreamatorium.Rendering;

public enum BufferVisualizationChannels
{
    RGB,
    A,
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class BufferVisualizationAttribute : Attribute
{
    public BufferVisualizationChannels Channels { get; }

    public string DisplayName { get; }

    public BufferVisualizationAttribute(string displayName)
    {
        DisplayName = displayName;
        Channels = BufferVisualizationChannels.RGB;
    }

    public BufferVisualizationAttribute(string displayName, BufferVisualizationChannels channels)
    {
        DisplayName = displayName;
        Channels = channels;
    }
}
