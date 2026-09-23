namespace cowsins
{
    // Implemented by PlayerStats and required by PlayerDependencies
    public interface IFallHeightProvider
    {
        float? CurrentFallHeight { get; }
        void SetFallHeight(float newFallHeight);
    }
}