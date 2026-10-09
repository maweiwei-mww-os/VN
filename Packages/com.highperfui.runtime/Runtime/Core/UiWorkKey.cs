using System;

namespace HighPerfUI
{
    public readonly struct UiWorkKey : IEquatable<UiWorkKey>
    {
        public readonly int View, Generation, Fragment, Kind;
        public UiWorkKey(int view, int generation, int fragment, int kind)
        { View = view; Generation = generation; Fragment = fragment; Kind = kind; }
        public bool Equals(UiWorkKey other) => View == other.View && Generation == other.Generation && Fragment == other.Fragment && Kind == other.Kind;
        public override bool Equals(object obj) => obj is UiWorkKey key && Equals(key);
        public override int GetHashCode() { unchecked { return (((View * 397) ^ Generation) * 397 ^ Fragment) * 397 ^ Kind; } }
    }
    public interface IKeyedUiWorkItem : IUiWorkItem { UiWorkKey Key { get; } }
}
