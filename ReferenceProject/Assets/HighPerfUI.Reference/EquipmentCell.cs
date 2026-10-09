using System;
using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI.Reference
{
    public sealed class EquipmentCell : IncrementalUiView
    {
        public Image Background, Stripe, Icon;
        public Text NameLabel, PowerLabel, LevelLabel;
        public GameObject[] FullFragments;
        public GameObject[] FragmentTemplates;
        public bool UseRuntime;
        private Equipment _item;
        private bool _selected, _equipped;
        private Action<long> _clicked;
        private AsyncSpriteSlot _slot;
        private LazyFragmentHost _lazy;
        private string _name;
        private int _power = -1, _level = -1, _quantity = -1, _kind = -1, _iconGeneration = -1;
        private bool _initialized;
        private readonly bool[] _flags = new bool[7];
        private readonly bool[] _appliedFlags = new bool[7];
        private int _rarity = -1, _mastery = -1, _set = -1;
        private long _expiry = -1;
        private static readonly string[] FragmentNames = { "Selected", "Locked", "Equipped", "Rare", "Mastery", "Set", "Expiry" };
        private const ulong NameDirty = 1, PowerDirty = 2, LevelDirty = 4, AppearanceDirty = 8, AllDirty = 15;
        public long AppliedId { get; private set; }
        public bool HasCurrentIcon => Icon.enabled && Icon.sprite != null && _iconGeneration == BindingGeneration;
        public static long BindCalls, ApplyCalls;
        public static double BindCpuMs, ApplyCpuMs, SynchronousApplyCpuMs;

        public void SetData(Equipment item, bool selected, bool equipped, UiRuntime runtime, EquipmentSpriteProvider provider, Action<long> clicked)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp(); BindCalls++;
            ConfigureRuntime(runtime); _item = item; _selected = selected; _equipped = equipped; _clicked = clicked;
            if (!_initialized)
            {
                _slot = gameObject.AddComponent<AsyncSpriteSlot>(); _slot.Configure(Icon, provider);
                GetComponent<Button>().onClick.AddListener(() => { if (IsCommitted) _clicked?.Invoke(DataId); });
                if (UseRuntime)
                {
                    _lazy = gameObject.AddComponent<LazyFragmentHost>();
                    var descriptors = new UiFragmentDescriptor[FragmentNames.Length];
                    for (int i = 0; i < descriptors.Length; i++) descriptors[i] = new UiFragmentDescriptor { Id = FragmentNames[i], Prefab = FragmentTemplates[i], Parent = transform, RequiredForInteraction = i < 3 };
                    _lazy.Configure(runtime, this, descriptors);
                }
                _initialized = true;
            }
            bool fresh = AppliedId != DataId;
            _flags[0] = selected; _flags[1] = item.Locked; _flags[2] = equipped;
            _flags[3] = item.Kind < 6 && item.Rarity == 4; _flags[4] = item.Mastery > 0; _flags[5] = item.SetId > 0; _flags[6] = item.ExpiresAtUtc > 0;
            ulong mask = fresh ? AllDirty : 0;
            if (_name != item.Name) mask |= NameDirty;
            if (_power != item.Power || _quantity != item.Quantity) mask |= PowerDirty;
            if (_level != item.Level) mask |= LevelDirty;
            if (_rarity != item.Rarity || _kind != item.Kind || _mastery != item.Mastery || _set != item.SetId || _expiry != item.ExpiresAtUtc) mask |= AppearanceDirty;
            for (int i = 0; i < _flags.Length; i++) if (_flags[i] != _appliedFlags[i]) mask |= AppearanceDirty;
            if (UseRuntime && !fresh) { if (mask != 0) MarkDirty(mask); }
            else { InvalidateState(); double before = ApplyCpuMs; ApplyDirty(mask); SynchronousApplyCpuMs += ApplyCpuMs - before; CommitBinding(); }
            BindCpuMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
        protected override void ApplyDirty(ulong mask)
        {
            if (_item == null) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp(); ApplyCalls++;
            if ((mask & NameDirty) != 0) { _name = _item.Name; NameLabel.text = _name; UiMetrics.DirtyApplied(); }
            if ((mask & PowerDirty) != 0) { _power = _item.Power; _quantity = _item.Quantity; PowerLabel.text = _item.Kind < 6 ? "战力 " + _power : "数量 " + _quantity; UiMetrics.DirtyApplied(); }
            if ((mask & LevelDirty) != 0) { _level = _item.Level; LevelLabel.text = _item.Kind < 6 ? "+" + _level : ""; UiMetrics.DirtyApplied(); }
            if (_kind != _item.Kind || _iconGeneration != BindingGeneration)
            { _kind = _item.Kind; _iconGeneration = BindingGeneration; _slot.Bind(_kind.ToString(), CaptureBinding()); }
            if ((mask & AppearanceDirty) != 0)
            {
                Background.color = _selected ? UiFactory.Hex("3B4C58") : UiFactory.Surface;
                Stripe.color = RarityColor(_item.Rarity);
                if (UseRuntime) _lazy.RequireVisibleState(_flags);
                for (int i = 0; i < _flags.Length; i++)
                {
                    var instance = UseRuntime ? _lazy.GetIfCreated(FragmentNames[i]) : FullFragments[i];
                    if (instance != null)
                    {
                        if (!UseRuntime) instance.SetActive(_flags[i]);
                        if (_flags[i]) instance.GetComponent<EquipmentFragmentPresentation>().Apply(_item);
                    }
                    _appliedFlags[i] = _flags[i];
                }
                if (UseRuntime) _lazy.TrimHidden(0);
                _rarity = _item.Rarity; _mastery = _item.Mastery; _set = _item.SetId; _expiry = _item.ExpiresAtUtc;
            }
            else if ((mask & PowerDirty) != 0 && _flags[3])
            {
                var rare = UseRuntime ? _lazy.GetIfCreated(FragmentNames[3]) : FullFragments[3];
                rare?.GetComponent<EquipmentFragmentPresentation>().Apply(_item);
            }
            AppliedId = DataId;
            ApplyCpuMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
        public override void OnReturn()
        {
            _slot?.ResetSlot(); _lazy?.ResetBinding();
            _clicked = null; _item = null; AppliedId = 0; _iconGeneration = -1;
            base.OnReturn();
        }
        public bool Matches(Equipment expected, bool selected, bool equipped, Sprite icon)
        {
            if (expected == null || DataId != expected.Id || AppliedId != expected.Id || !IsCommitted ||
                NameLabel.text != expected.Name || PowerLabel.text != (expected.Kind < 6 ? "战力 " + expected.Power : "数量 " + expected.Quantity) ||
                LevelLabel.text != (expected.Kind < 6 ? "+" + expected.Level : "") || Icon.sprite != icon || !Icon.enabled) return false;
            bool[] flags = { selected, expected.Locked, equipped, expected.Kind < 6 && expected.Rarity == 4, expected.Mastery > 0, expected.SetId > 0, expected.ExpiresAtUtc > 0 };
            for (int i = 0; i < flags.Length; i++)
            {
                var fragment = UseRuntime ? _lazy.GetIfCreated(FragmentNames[i]) : FullFragments[i];
                if ((fragment != null && fragment.activeSelf) != flags[i]) return false;
            }
            return true;
        }
        public static Color RarityColor(int rarity)
        { return rarity == 4 ? UiFactory.Hex("E8BE68") : rarity == 3 ? UiFactory.Hex("BC96D5") : rarity == 2 ? UiFactory.Hex("68ACC7") : UiFactory.Hex("8DBB9A"); }
    }
}
