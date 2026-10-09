using System;

namespace HighPerfUI
{
    public sealed class UiPoolBudget
    {
        public int Maximum { get; }
        public int Retained { get; private set; }
        public UiPoolBudget(int maximum) { Maximum = Math.Max(0, maximum); }
        internal bool TryRetain() { if (Retained >= Maximum) return false; Retained++; return true; }
        internal void Release() { Retained = Math.Max(0, Retained - 1); }
    }
}
