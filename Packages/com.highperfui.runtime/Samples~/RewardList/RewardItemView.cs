using HighPerfUI;
using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI.Samples.RewardList
{
    public sealed class RewardItemView : IncrementalUiView
    {
        private const ulong DirtyName = 1UL << 0;
        private const ulong DirtyCount = 1UL << 1;
        private const ulong DirtyClaimed = 1UL << 2;
        private const ulong DirtyIcon = 1UL << 3;

        [SerializeField] private Text _nameText;
        [SerializeField] private Text _countText;
        [SerializeField] private GameObject _claimedMark;
        [SerializeField] private AsyncSpriteSlot _iconSlot;

        private string _targetName;
        private int _targetCount;
        private bool _targetClaimed;
        private string _targetIconKey;

        private string _committedName;
        private int _committedCount;
        private bool _committedClaimed;
        private string _committedIconKey;

        public override void BeginBind(long dataId)
        {
            base.BeginBind(dataId);
            // A new logical item must commit all critical fields even if its values happen
            // to equal the previous item's values.
            MarkDirty(DirtyName | DirtyCount | DirtyClaimed | DirtyIcon);
        }

        public void SetData(string displayName, int count, bool claimed, string iconKey)
        {
            ulong dirty = 0;
            if (_targetName != displayName) { _targetName = displayName; dirty |= DirtyName; }
            if (_targetCount != count) { _targetCount = count; dirty |= DirtyCount; }
            if (_targetClaimed != claimed) { _targetClaimed = claimed; dirty |= DirtyClaimed; }
            if (_targetIconKey != iconKey) { _targetIconKey = iconKey; dirty |= DirtyIcon; }
            if (dirty != 0) MarkDirty(dirty);
        }

        protected override void ApplyDirty(ulong dirtyMask)
        {
            if ((dirtyMask & DirtyName) != 0)
            {
                _committedName = _targetName;
                if (_nameText != null) _nameText.text = _committedName;
            }
            if ((dirtyMask & DirtyCount) != 0)
            {
                _committedCount = _targetCount;
                if (_countText != null) _countText.text = _committedCount.ToString();
            }
            if ((dirtyMask & DirtyClaimed) != 0)
            {
                _committedClaimed = _targetClaimed;
                if (_claimedMark != null) _claimedMark.SetActive(_committedClaimed);
            }
            if ((dirtyMask & DirtyIcon) != 0)
            {
                _committedIconKey = _targetIconKey;
                if (_iconSlot != null) _iconSlot.Bind(_committedIconKey, CaptureBinding());
            }
        }

        public override void OnReturn()
        {
            if (_iconSlot != null) _iconSlot.ResetSlot();
            base.OnReturn();
        }
    }
}
