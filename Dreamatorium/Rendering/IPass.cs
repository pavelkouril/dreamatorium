using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public interface IPass
{
    public void Execute(MTL4CommandBuffer commandBuffer);
}

public interface IPass<TSettings> : IPass
{
    public TSettings Settings { get; }
}
