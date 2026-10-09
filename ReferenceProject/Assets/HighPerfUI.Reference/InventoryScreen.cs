using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HighPerfUI.Reference
{
    public sealed partial class InventoryScreen : MonoBehaviour, IVirtualizedItemSource
    {
        public InventoryModel Model { get; private set; }
        public FixedVirtualizedScrollList Collection { get; private set; }
        public UiRuntime Runtime { get; private set; }
        public EquipmentSpriteProvider Provider { get; private set; }
        public int Mode { get; private set; } = 2;
        public double OpenMs { get; private set; }
        public int NativeNodes { get; private set; }
        private bool _measuring;
        private CanvasGroup _inputGate;
        public bool Measuring
        {
            get => _measuring;
            set
            {
                _measuring = value;
                if (_inputGate != null) _inputGate.blocksRaycasts = !value;
                if (value && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
        }
        public long SelectedId { get; private set; } = 1;
        public string AbortReason { get; private set; }
        public int Count => _filtered.Count;
        public bool Ready
        {
            get
            {
                if (Collection == null || Collection.PendingCount != 0 || Runtime.Scheduler.PendingCount != 0) return false;
                foreach (var view in Collection.ActiveViews)
                    if (!view.IsCommitted || !((EquipmentCell)view).HasCurrentIcon) return false;
                return _filtered.Count == 0 || Collection.ActiveCount > 0;
            }
        }
        private readonly List<Equipment> _filtered = new List<Equipment>();
        private readonly List<float> _frames = new List<float>(240);
        private RectTransform _root, _listArea, _templates, _modal;
        private EquipmentCell _fullPrefab, _lazyPrefab;
        private Text _detailTitle, _detailStats, _detailType, _power, _status, _countLabel, _metrics, _setProgress;
        private Image _detailIcon;
        private Button[] _modeButtons, _kindButtons, _slots;
        private int _kindFilter = -1;
        private bool _sortPower;
        private string _query = "";
        private Stopwatch _openWatch;
        private float _nextStats;
        private BenchmarkRunner _benchmark;

        private void Start()
        {
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120; Application.runInBackground = true;
            int count = ArgInt("--count", 1000);
            Model = new InventoryModel(count);
            InitializeBusinessState();
            var runtimeObject = new GameObject("UI Runtime"); runtimeObject.transform.SetParent(transform); Runtime = runtimeObject.AddComponent<UiRuntime>();
            Runtime.FrameBudgetMs = 2;
            Provider = gameObject.AddComponent<EquipmentSpriteProvider>();
            if (FindObjectOfType<EventSystem>() == null)
            { var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(transform); }
            BuildShell(); BuildTemplates(); Refilter(false);
            int mode = Arg("--mode", "C")[0] - 'A';
            SetMode(Mathf.Clamp(mode, 0, 2));
            _benchmark = gameObject.AddComponent<BenchmarkRunner>(); _benchmark.Initialize(this);
            if (HasArg("--benchmark")) _benchmark.BeginCommandLine();
        }

        private void BuildShell()
        {
            var canvasObject = new GameObject("Inventory Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = UiFactory.Panel("Background", canvasObject.transform, 0, 0, 1440, 900, UiFactory.Background); UiFactory.Stretch(background.rectTransform);
            _root = UiFactory.Rect("Inventory", canvasObject.transform, 0, 0, 1440, 900);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(.5f, .5f); _root.anchoredPosition = Vector2.zero;
            _inputGate = _root.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Panel("Header", _root, 0, 0, 1440, 76, UiFactory.Hex("22252A"));
            UiFactory.Label(_root, "军备整备中心", 26, 17, 246, 38, 26);
            UiFactory.Label(_root, "守誓军团 · 第三赛季", 287, 24, 238, 26, 14, UiFactory.Muted);
            _wallet = UiFactory.Label(_root, "", 556, 20, 460, 36, 17, UiFactory.Accent, TextAnchor.MiddleRight);
            UiFactory.Button(_root, "奖励中心", 1040, 20, 124, 36, () => { if (!Measuring) OpenRewardCenter(); }, UiFactory.Hex("376071"));
            UiFactory.Button(_root, "性能对照", 1177, 20, 132, 36, () => ToggleDiagnostics());
            UiFactory.Button(_root, "关闭", 1321, 20, 90, 36, () => { SaveProgress(); Application.Quit(); });

            UiFactory.Label(_root, "角色配装", 26, 97, 228, 30, 19);
            UiFactory.Label(_root, "艾琳 · 守誓者", 26, 144, 222, 33, 24);
            UiFactory.Label(_root, "先锋  /  Lv.60", 26, 184, 222, 25, 14, UiFactory.Muted);
            var crest = UiFactory.Panel("Commander", _root, 26, 215, 228, 140, Color.white);
            crest.sprite = CommanderPortrait(); crest.preserveAspect = true;
            _power = UiFactory.Label(_root, "", 26, 364, 230, 45, 26, UiFactory.Accent);
            UiFactory.Label(_root, "已装备", 26, 424, 228, 25, 15, UiFactory.Muted);
            _slots = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                int slot = i;
                _slots[i] = UiFactory.Button(_root, InventoryModel.KindNames[i], 26 + i % 2 * 116, 463 + i / 2 * 55, 108, 46, () => SelectItem(Model.Equipped[slot]));
            }
            UiFactory.Label(_root, "守誓套装", 26, 642, 220, 26, 17);
            _setProgress = UiFactory.Label(_root, "", 26, 674, 220, 40, 14, UiFactory.Accent);
            UiFactory.Panel("Divider", _root, 267, 99, 1, 604, UiFactory.Border);

            _kindButtons = new Button[7];
            for (int i = 0; i < 7; i++)
            {
                int kind = i - 1;
                _kindButtons[i] = UiFactory.Button(_root, i == 0 ? "全部" : InventoryModel.KindNames[i - 1], 286 + i * 113, 99, 106, 33,
                    () => { if (!Measuring) { _kindFilter = kind; Refilter(); } });
            }
            var search = UiFactory.Input(_root, "搜索装备", 286, 148, 276, 36);
            search.onValueChanged.AddListener(value => { if (!Measuring) { _query = value; Refilter(); } });
            UiFactory.Button(_root, "按品质", 574, 148, 105, 36, () => { if (!Measuring) { _sortPower = false; Refilter(); } });
            UiFactory.Button(_root, "按战力", 686, 148, 105, 36, () => { if (!Measuring) { _sortPower = true; Refilter(); } });
            _countLabel = UiFactory.Label(_root, "", 804, 148, 127, 36, 14, UiFactory.Muted, TextAnchor.MiddleRight);
            UiFactory.Checkbox(_root, "仅锁定", 947, 148, 118, value => { if (!Measuring) { _lockedOnly = value; Refilter(); } });
            _listArea = UiFactory.Rect("Collection area", _root, 286, 201, 784, 495);
            UiFactory.Panel("Divider", _root, 1090, 99, 1, 604, UiFactory.Border);

            UiFactory.Label(_root, "装备详情", 1110, 98, 296, 30, 19);
            _detailIcon = UiFactory.Panel("Detail icon", _root, 1174, 145, 154, 143, Color.white); _detailIcon.preserveAspect = true;
            _detailTitle = UiFactory.Label(_root, "", 1110, 304, 294, 38, 24);
            _detailType = UiFactory.Label(_root, "", 1110, 349, 294, 30, 14, UiFactory.Muted);
            _detailStats = UiFactory.Label(_root, "", 1110, 400, 294, 175, 17);
            _equipButton = UiFactory.Button(_root, "穿戴装备", 1110, 594, 294, 42, () => { if (!Measuring) EquipSelected(); }, UiFactory.Hex("376071"));
            _upgradeButton = UiFactory.Button(_root, "强化", 1110, 648, 141, 39, () => { if (!Measuring) ShowUpgrade(); });
            UiFactory.Button(_root, "锁定 / 解锁", 1263, 648, 141, 39, () => { if (!Measuring && !Model.ToggleLock(SelectedId)) SetStatus(Model.LastError); });
            BuildBusinessFooter(); BuildDiagnostics();
            _status = UiFactory.Label(_root, "", 26, 869, 1204, 26, 13, UiFactory.Muted);
            UiFactory.Button(_root, "保存进度", 1280, 867, 131, 28, SaveProgress);
        }

        private void BuildTemplates()
        {
            _templates = UiFactory.Rect("Shared templates", transform, 0, 0, 160, 160); _templates.gameObject.SetActive(false);
            var fragments = new GameObject[7];
            for (int i = 0; i < fragments.Length; i++) fragments[i] = EquipmentFragmentPresentation.Create(i, _templates);
            _fullPrefab = BuildCell(false, fragments); _lazyPrefab = BuildCell(true, fragments);
        }
        private EquipmentCell BuildCell(bool lazy, GameObject[] fragments)
        {
            var image = UiFactory.Panel(lazy ? "Lazy cell" : "Full cell", _templates, 0, 0, 151, 160, UiFactory.Surface);
            image.raycastTarget = true; image.gameObject.AddComponent<Button>().targetGraphic = image;
            var gate = image.gameObject.AddComponent<CanvasGroup>();
            var cell = image.gameObject.AddComponent<EquipmentCell>(); cell.ConfigureInteraction(gate);
            cell.Background = image; cell.UseRuntime = lazy; cell.FragmentTemplates = fragments;
            cell.Stripe = UiFactory.Panel("Quality", image.transform, 0, 0, 151, 3, UiFactory.Accent);
            cell.Icon = UiFactory.Panel("Icon", image.transform, 35, 15, 82, 84, Color.white); cell.Icon.preserveAspect = true;
            cell.NameLabel = UiFactory.Label(image.transform, "", 9, 108, 133, 24, 15);
            cell.PowerLabel = UiFactory.Label(image.transform, "", 9, 135, 110, 20, 12, UiFactory.Muted);
            cell.LevelLabel = UiFactory.Label(image.transform, "", 111, 106, 30, 24, 13, UiFactory.Accent, TextAnchor.MiddleRight);
            cell.FullFragments = new GameObject[fragments.Length];
            if (!lazy) for (int i = 0; i < fragments.Length; i++) { cell.FullFragments[i] = Instantiate(fragments[i], image.transform, false); cell.FullFragments[i].SetActive(false); }
            image.gameObject.SetActive(false); return cell;
        }

        public void SetMode(int mode)
        {
            Mode = Mathf.Clamp(mode, 0, 2); AbortReason = null;
            if (Collection != null) { Collection.SetSource(null); Collection.gameObject.SetActive(false); Destroy(Collection.gameObject); }
            Runtime.Scheduler.Clear(); UiMetrics.Reset(); OpenMs = -1; NativeNodes = 0; _frames.Clear();
            EquipmentCell.BindCalls = EquipmentCell.ApplyCalls = 0;
            EquipmentCell.BindCpuMs = EquipmentCell.ApplyCpuMs = EquipmentCell.SynchronousApplyCpuMs = 0;
            for (int i = 0; i < 3; i++) _modeButtons[i].image.color = i == Mode ? UiFactory.Hex("426A51") : UiFactory.Border;
            if (Mode == 0 && Count > 3000) { AbortReason = "A 模式超过 3000 项保护上限，本轮中止"; _status.text = AbortReason; return; }
            _openWatch = Stopwatch.StartNew();
            var listRoot = UiFactory.Rect("Equipment grid", _listArea, 0, 0, 784, 495);
            var scroll = listRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            var viewport = UiFactory.Rect("Viewport", listRoot, 0, 0, 784, 495); viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect("Content", viewport, 0, 0, 784, 495); scroll.viewport = viewport; scroll.content = content;
            Collection = listRoot.gameObject.AddComponent<FixedVirtualizedScrollList>();
            Collection.Virtualize = Mode != 0; Collection.ScheduleWork = Mode == 2;
            Collection.Configure(scroll, viewport, content, Mode == 2 ? _lazyPrefab : _fullPrefab, Runtime, 5, 160, 7);
            Collection.SetSource(this); UpdateDetails();
            _status.text = "军备库准备中";
        }
        public void ResetDataset(int count)
        { Model.Changed -= OnModelChanged; Model = new InventoryModel(count); Model.Changed += OnModelChanged; _knownItemCount = count; SelectedId = count > 0 ? Model.Items[0].Id : 0; _kindFilter = -1; _query = ""; _sortPower = false; _lockedOnly = false; Refilter(false); }
        private void Refilter(bool apply = true)
        {
            _filtered.Clear();
            foreach (var item in Model.Items)
                if ((_kindFilter < 0 || item.Kind == _kindFilter) && (!_lockedOnly || item.Locked) && (string.IsNullOrEmpty(_query) || item.Name.Contains(_query))) _filtered.Add(item);
            _filtered.Sort((a, b) => { int value = _sortPower ? b.Power.CompareTo(a.Power) : b.Rarity.CompareTo(a.Rarity); return value != 0 ? value : a.Id.CompareTo(b.Id); });
            _countLabel.text = _filtered.Count + " / " + Model.Items.Count + " 件";
            for (int i = 0; i < 7; i++) _kindButtons[i].image.color = i == _kindFilter + 1 ? UiFactory.Hex("426A51") : UiFactory.Border;
            if (apply && Collection != null) { Collection.RefreshStructure(); Collection.ScrollTo(0); }
        }
        public long GetStableId(int index) => _filtered[index].Id;
        public void Bind(PooledUiView view, int index)
        {
            var item = _filtered[index];
            ((EquipmentCell)view).SetData(item, item.Id == SelectedId, IsEquipped(item), Runtime, Provider, SelectItem);
        }
        public void Unbind(PooledUiView view, int index) { }
        public void SelectItem(long id) { if (Model.Find(id) == null) return; long previous = SelectedId; SelectedId = id; RefreshBusiness(new[] { previous, id }); }
        public void EquipSelected() { if (!Model.Equip(SelectedId)) SetStatus(Model.LastError); }
        public void UpgradeSelected() { if (!Model.Upgrade(SelectedId)) SetStatus(Model.LastError); }
        public void BatchUpdate()
        { _changedIds.Clear(); for (int i = 0; i < Model.Items.Count; i += 3) { Model.Items[i].Power++; _changedIds.Add(Model.Items[i].Id); } RefreshBusiness(_changedIds); }
        private void RefreshBusiness(IEnumerable<long> ids)
        { if (Mode == 2) Collection?.RefreshItems(ids); else Collection?.RefreshVisible(); UpdateDetails(); }
        private void UpdateDetails()
        {
            var item = Model.Find(SelectedId); if (item == null) return;
            _detailTitle.text = item.Name; _detailTitle.color = EquipmentCell.RarityColor(item.Rarity);
            _detailIcon.sprite = Provider.Icons[item.Kind];
            _detailType.text = InventoryModel.KindNames[item.Kind] + "  /  品质 " + item.Rarity + (item.Kind < 6 ? "  /  强化 +" + item.Level : "  /  数量 " + item.Quantity);
            int compare = item.Kind < 6 ? item.Power - (Model.Find(Model.Equipped[item.Kind])?.Power ?? 0) : 0;
            _detailStats.text = item.Kind < 6 ? "战力        " + item.Power + "\n攻击        +" + item.Power / 3 + "\n防御        +" + item.Power / 5 + "\n替换提升    " + (compare >= 0 ? "+" : "") + compare +
                "\n\n" + (item.SetId > 0 ? "守誓套装 " + item.SetId : "散件装备") + "  /  精通 " + item.Mastery + "%" : "赛季奖励物品\n\n数量        " + item.Quantity + "\n\n" + (item.Kind == 7 ? "英雄信物，作为军团收藏保存在仓库。" : "作为军团物资保存在仓库中。");
            _equipButton.interactable = item.Kind < 6 && !IsEquipped(item);
            _upgradeButton.interactable = item.Kind < 6 && item.Level < 30;
            _power.text = "战力  " + Model.TotalPower.ToString("N0");
            _setProgress.text = Model.SetPieces + " / 2 件  " + (Model.SetPieces >= 2 ? "防御 +12%" : "未激活") + "\n总防御  " + Model.TotalDefense;
            for (int i = 0; i < 6; i++) _slots[i].GetComponentInChildren<Text>().text = InventoryModel.KindNames[i] + " +" + (Model.Find(Model.Equipped[i])?.Level ?? 0);
            _wallet.text = "金币  " + Model.Gold.ToString("N0") + "       精铁  " + Model.Ore.ToString("N0");
        }
        private void ShowUpgrade()
        {
            if (_modal != null) Destroy(_modal.gameObject);
            var shade = UiFactory.Panel("Modal shade", _root, 0, 0, 1440, 900, new Color(0, 0, 0, .78f)); shade.raycastTarget = true; _modal = shade.rectTransform;
            var box = UiFactory.Panel("Upgrade", _modal, 495, 270, 450, 340, UiFactory.Surface);
            var item = Model.Find(SelectedId);
            UiFactory.Label(box.transform, "强化装备", 28, 22, 394, 36, 24);
            long targetId = item.Id; string operationId = Guid.NewGuid().ToString("N");
            UiFactory.Label(box.transform, item.Name + "\n\n等级  +" + item.Level + "  →  +" + Mathf.Min(30, item.Level + 1) + "\n战力  +" + (12 + item.Rarity * 3) + "\n金币  " + Model.UpgradeCost(item.Id) + "   /   精铁  " + Model.UpgradeOreCost(item.Id), 28, 77, 394, 164, 18);
            UiFactory.Button(box.transform, "取消", 28, 268, 185, 43, CloseModal);
            UiFactory.Button(box.transform, "确认强化", 237, 268, 185, 43, () => { bool success = Model.TryUpgrade(targetId, operationId); CloseModal(); SetStatus(success ? "强化成功，装备和军团属性已更新" : Model.LastError); }, UiFactory.Hex("376071"));
        }
        public void ShowReport(string report)
        {
            if (_modal != null) Destroy(_modal.gameObject);
            var shade = UiFactory.Panel("Report shade", _root, 0, 0, 1440, 900, new Color(0, 0, 0, .86f)); shade.raycastTarget = true; _modal = shade.rectTransform;
            UiFactory.Panel("Report", _modal, 90, 130, 1260, 630, UiFactory.Surface);
            UiFactory.Label(_modal, "性能对比报告", 122, 158, 1130, 46, 27);
            UiFactory.Label(_modal, report, 122, 228, 1180, 420, 18);
            UiFactory.Button(_modal, "返回装备库", 1120, 686, 190, 42, () => Destroy(_modal.gameObject));
        }
        public void SetStatus(string status) { _status.text = status; }
        public Rect GridPixelRect()
        {
            var corners = new Vector3[4]; _listArea.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        public void ShowMeasurement(BenchmarkResult result)
        {
            _metrics.text = "整帧 P95      " + result.FrameP95Ms.ToString("F2") + " ms        首屏可交互      " + result.OpenMs.ToString("F1") +
                " ms        Item 实例      " + result.ActiveViews + "        节点总数      " + result.NativeNodes + "\n\n" +
                "创建次数      " + result.Metrics.Instantiations + "        托管内存      " + (result.MonoBytes / 1048576.0).ToString("F1") +
                " MB        有效帧      " + result.Frames + "        业务一致性      " + (result.VisibleStateCorrect ? "通过" : "失败");
        }
        private void Update()
        {
            if (Collection == null) return;
            RecordOpeningIfReady();
            TickBusinessSave();
            if (Measuring || _diagnostics == null || !_diagnostics.activeSelf) return;
            if (_frames.Count == 240) _frames.RemoveAt(0);
            _frames.Add(Time.unscaledDeltaTime * 1000);
            if (Time.unscaledTime < _nextStats) return;
            _nextStats = Time.unscaledTime + .5f;
            var samples = _frames.ToArray(); Array.Sort(samples);
            float p95 = samples.Length > 0 ? samples[Mathf.Min(samples.Length - 1, Mathf.CeilToInt(samples.Length * .95f) - 1)] : 0;
            var metrics = UiMetrics.Snapshot();
            _metrics.text = "整帧 P95      " + p95.ToString("F2") + " ms        首屏可交互      " + (OpenMs < 0 ? "采集中" : OpenMs.ToString("F1") + " ms") +
                "        Item 实例      " + Collection.ActiveCount + "        创建次数      " + metrics.Instantiations + "\n\n" +
                "调度耗时      " + Runtime.Scheduler.LastFrameMs.ToString("F2") + " ms        队列      " + Runtime.Scheduler.PendingCount +
                "        托管内存      " + (Profiler.GetMonoUsedSizeLong() / 1048576.0).ToString("F1") + " MB        失效回调      " + metrics.AsyncStaleDropped;
        }
        internal void RecordOpeningIfReady()
        {
            if (OpenMs < 0 && Ready)
            {
                OpenMs = _openWatch.Elapsed.TotalMilliseconds;
                NativeNodes = Collection.GetComponentsInChildren<Transform>(true).Length;
                _status.text = string.IsNullOrEmpty(_saveWarning) ? "军备库已就绪  /  " + Count + " 件物品" : _saveWarning;
            }
        }
        public static string Arg(string name, string fallback)
        { var args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1]; return fallback; }
        public static int ArgInt(string name, int fallback) => int.TryParse(Arg(name, ""), out int value) ? value : fallback;
        public static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
    }
}
