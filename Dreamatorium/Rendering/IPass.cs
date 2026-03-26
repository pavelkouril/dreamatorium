namespace Dreamatorium.Rendering;

public interface IPass
{
    public void Execute();
}

public interface IPass<TSettings> : IPass
{
    public TSettings Settings { get; }
}
