using HighPerfUI;
using UnityEngine.UI;

namespace HighPerfUI.Samples
{
    public sealed class MinimalItemView : IncrementalUiView
    {
        public Text Label;
        private int _value;
        public void SetValue(int value, UiRuntime runtime) { ConfigureRuntime(runtime); _value = value; MarkDirty(1); }
        protected override void ApplyDirty(ulong mask) { Label.text = "Item " + _value; }
    }
}
