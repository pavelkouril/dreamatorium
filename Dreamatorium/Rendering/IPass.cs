using SharpMetal.Metal;

namespace Dreamatorium.Rendering;

public interface IPass
{
    public void Execute(MTLCommandBuffer commandBuffer);
}

public interface IPass<TSettings> : IPass
{
    public TSettings Settings { get; }
}
