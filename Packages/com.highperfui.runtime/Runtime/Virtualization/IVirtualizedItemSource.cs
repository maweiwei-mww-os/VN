namespace HighPerfUI
{
    public interface IVirtualizedItemSource
    {
        int Count { get; }
        long GetStableId(int index);
        void Bind(PooledUiView view, int index);
        void Unbind(PooledUiView view, int index);
    }
}
