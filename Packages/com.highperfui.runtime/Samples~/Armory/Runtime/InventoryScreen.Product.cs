using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI.Reference
{
    public sealed partial class InventoryScreen
    {
        private readonly List<long> _changedIds = new List<long>();
        private readonly List<EquipmentCell> _rewardViews = new List<EquipmentCell>();
        private GameObject _diagnostics, _businessFooter;
        private Text _wallet;
        private Button _equipButton, _upgradeButton;
        private Sprite _portrait;
        private bool _lockedOnly, _saveDirty;
        private float _saveAt;
        private int _knownItemCount;
        private string _saveWarning;
        public string SavePath => Path.Combine(Application.persistentDataPath, "armory-v3.json");
        private bool PersistentSession => !Application.isEditor && !HasArg("--benchmark") && !HasArg("--fresh");

        internal sealed class BusinessSession
        {
            internal InventoryModel Model;
            internal long Selected;
            internal int Mode, Kind;
            internal bool Locked, Sort, Dirty;
            internal string Query, Warning;
            internal float SaveAt;
        }
        internal BusinessSession CaptureBusinessSession() => new BusinessSession {
            Model = Model, Selected = SelectedId, Mode = Mode, Kind = _kindFilter, Locked = _lockedOnly,
            Sort = _sortPower, Dirty = _saveDirty, Query = _query, Warning = _saveWarning, SaveAt = _saveAt
        };
        internal void RestoreBusinessSession(BusinessSession session)
        {
            Model.Changed -= OnModelChanged; Model = session.Model; Model.Changed += OnModelChanged;
            SelectedId = session.Selected; _kindFilter = session.Kind; _lockedOnly = session.Locked;
            _sortPower = session.Sort; _query = session.Query; _knownItemCount = Model.Items.Count;
            _saveDirty = session.Dirty; _saveWarning = session.Warning; _saveAt = session.SaveAt;
            Refilter(false); SetMode(session.Mode);
        }

        private void InitializeBusinessState()
        {
            if (PersistentSession)
            {
                var restored = InventorySaveStore.Load(SavePath, out _saveWarning);
                if (restored != null) Model = restored;
            }
            _knownItemCount = Model.Items.Count;
            SelectedId = Model.Items.Count > 0 ? Model.Items[0].Id : 0;
            Model.Changed += OnModelChanged;
        }

        private void OnModelChanged(long[] ids)
        {
            bool structural = Model.Items.Count != _knownItemCount || _lockedOnly || _sortPower;
            _knownItemCount = Model.Items.Count;
            if (structural)
            {
                Refilter(false); Collection?.RefreshStructure();
            }
            RefreshBusiness(ids);
            _saveDirty = true; _saveAt = Time.unscaledTime + .6f;
        }
        private static bool IsEquipment(Equipment item) => item != null && item.Kind >= 0 && item.Kind < 6;
        public bool IsEquipped(Equipment item) => IsEquipment(item) && Model.Equipped[item.Kind] == item.Id;

        private void TickBusinessSave()
        {
            if (!PersistentSession || Measuring || !_saveDirty || Time.unscaledTime < _saveAt) return;
            SaveProgress();
        }
        public void SaveProgress()
        {
            if (!PersistentSession) { SetStatus("演示与验证模式不覆盖本地进度"); return; }
            try
            {
                InventorySaveStore.Save(SavePath, Model); _saveDirty = false; _saveWarning = null;
                SetStatus("本地进度已保存");
            }
            catch (Exception e)
            {
                _saveDirty = true;
                _saveAt = Time.unscaledTime + 30;
                _saveWarning = "保存失败，30秒后重试；当前进度仍保留：" + e.Message;
                SetStatus(_saveWarning);
            }
        }
        private void RecoverLocalSave()
        {
            if (!PersistentSession) { SetStatus("演示模式不修改本地存档"); return; }
            try
            {
                string quarantine = InventorySaveStore.RecoverPrimary(SavePath);
                SaveProgress();
                if (!_saveDirty) SetStatus(quarantine.Length > 0 ? "存档已恢复，损坏原件已隔离保留" : "存档校验正常");
            }
            catch (Exception e) { _saveAt = Time.unscaledTime + 30; SetStatus("无法恢复，原文件保留：" + e.Message); }
        }
        private void OnApplicationQuit() { if (_saveDirty) SaveProgress(); }
        private void OnDestroy()
        {
            if (Model != null) Model.Changed -= OnModelChanged;
            if (_portrait != null) Destroy(_portrait);
        }
        private Sprite CommanderPortrait()
        {
            var texture = Resources.Load<Texture2D>("ArmoryArt/Commander");
            if (texture == null) return Provider.Icons[2];
            _portrait = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            return _portrait;
        }

        private void BuildBusinessFooter()
        {
            _businessFooter = UiFactory.Panel("Season rewards", _root, 0, 722, 1440, 141, UiFactory.Hex("22252A")).gameObject;
            UiFactory.Label(_businessFooter.transform, "赛季补给", 26, 11, 160, 26, 18, UiFactory.Accent);
            var rewards = RewardCatalog.Entries;
            for (int i = 0; i < Math.Min(3, rewards.Count); i++)
            {
                var reward = rewards[i]; float x = 26 + i * 475;
                if (i > 0) UiFactory.Panel("Separator", _businessFooter.transform, x - 14, 49, 1, 77, UiFactory.Border);
                var icon = UiFactory.Panel("Reward icon", _businessFooter.transform, x, 52, 54, 64, Color.white);
                icon.sprite = Provider.Icons[reward.EquipmentKinds[0]]; icon.preserveAspect = true;
                UiFactory.Label(_businessFooter.transform, reward.Title, x + 67, 48, 244, 29, 18);
                UiFactory.Label(_businessFooter.transform, "金币 " + reward.Gold.ToString("N0") + "  ·  精铁 " + reward.Ore, x + 67, 83, 270, 24, 14, UiFactory.Muted);
                UiFactory.Button(_businessFooter.transform, "查看奖励", x + 332, 62, 112, 37, OpenRewardCenter);
            }
        }

        private void BuildDiagnostics()
        {
            _diagnostics = UiFactory.Panel("Diagnostics", _root, 0, 722, 1440, 141, UiFactory.Hex("22252A")).gameObject;
            UiFactory.Label(_diagnostics.transform, "性能对照", 26, 13, 139, 35, 18);
            _modeButtons = new Button[3];
            string[] modes = { "A 全量创建", "B 循环列表", "C 按需节点" };
            for (int i = 0; i < 3; i++)
            {
                int mode = i;
                _modeButtons[i] = UiFactory.Button(_diagnostics.transform, modes[i], 165 + i * 151, 13, 143, 35,
                    () => { if (!Measuring) SetMode(mode); });
            }
            UiFactory.Button(_diagnostics.transform, "运行三组对比", 634, 13, 168, 35, () => _benchmark?.BeginComparison(), UiFactory.Hex("376071"));
            UiFactory.Button(_diagnostics.transform, "测试报告", 815, 13, 122, 35, () => _benchmark?.ShowReports());
            UiFactory.Button(_diagnostics.transform, "修复本地存档", 950, 13, 155, 35, RecoverLocalSave);
            UiFactory.Button(_diagnostics.transform, "返回业务", 1280, 13, 130, 35, ToggleDiagnostics);
            _metrics = UiFactory.Label(_diagnostics.transform, "", 26, 58, 1380, 76, 15);
            _diagnostics.SetActive(false);
        }
        public void ToggleDiagnostics()
        {
            if (Measuring) return;
            bool show = !_diagnostics.activeSelf;
            _diagnostics.SetActive(show); _businessFooter.SetActive(!show);
        }
        internal void CloseModal()
        {
            _rewardViews.Clear();
            if (_modal == null) return;
            _modal.gameObject.SetActive(false); Destroy(_modal.gameObject); _modal = null;
        }

        public void OpenRewardCenter()
        {
            if (Measuring) return;
            CloseModal();
            var shade = UiFactory.Panel("Reward shade", _root, 0, 0, 1440, 900, new Color(0, 0, 0, .8f));
            shade.raycastTarget = true; _modal = shade.rectTransform;
            var body = UiFactory.Panel("Reward center", _modal, 94, 98, 1252, 704, UiFactory.Surface);
            UiFactory.Label(body.transform, "赛季奖励中心", 28, 22, 450, 38, 25, UiFactory.Accent);
            UiFactory.Label(body.transform, "守誓军团 · 军备补给", 740, 25, 475, 30, 16, UiFactory.Muted, TextAnchor.MiddleRight);
            var area = UiFactory.Rect("Reward list", body.transform, 25, 81, 1202, 529);
            var scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = UiFactory.Rect("Viewport", area, 0, 0, 1202, 529); viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect("Content", viewport, 0, 0, 1202, RewardCatalog.Entries.Count * 186);
            scroll.viewport = viewport; scroll.content = content;
            for (int i = 0; i < RewardCatalog.Entries.Count; i++) BuildRewardRow(content, RewardCatalog.Entries[i], i * 186);
            UiFactory.Button(body.transform, "返回军备库", 1000, 642, 220, 39, CloseModal);
            UiFactory.Label(body.transform, "军团物资", 28, 642, 180, 39, 16, UiFactory.Muted);
        }

        private void BuildRewardRow(Transform parent, RewardDefinition reward, float y)
        {
            var row = UiFactory.Rect("Reward " + reward.Id, parent, 0, y, 1202, 180);
            UiFactory.Label(row, reward.Title, 3, 7, 273, 33, 21);
            UiFactory.Label(row, reward.Description, 3, 44, 269, 55, 14, UiFactory.Muted);
            UiFactory.Label(row, "金币 " + reward.Gold.ToString("N0") + "\n精铁 " + reward.Ore, 3, 107, 269, 56, 15, UiFactory.Accent);
            var preview = Model.PreviewReward(reward.Id);
            for (int i = 0; i < preview.Length; i++)
            {
                var cell = Instantiate(Mode == 2 ? _lazyPrefab : _fullPrefab, row, false);
                var rect = (RectTransform)cell.transform;
                rect.anchoredPosition = new Vector2(291 + i * 139, -9); rect.sizeDelta = new Vector2(151, 160); rect.localScale = Vector3.one * .84f;
                cell.OnRent(); cell.BeginBind(preview[i].Id); cell.gameObject.SetActive(true);
                cell.SetData(preview[i], false, false, Runtime, Provider, null); cell.GetComponent<Button>().interactable = false;
                _rewardViews.Add(cell);
            }
            bool claimed = Model.IsRewardClaimed(reward.Id);
            var button = UiFactory.Button(row, claimed ? "已领取" : "领取奖励", 1044, 63, 149, 42, null, UiFactory.Hex("376071"));
            button.interactable = !claimed;
            button.onClick.AddListener(() =>
            {
                bool result = Model.ClaimReward(reward.Id);
                if (result || Model.IsRewardClaimed(reward.Id))
                {
                    button.interactable = false; button.GetComponentInChildren<Text>().text = "已领取";
                    SetStatus("奖励已入库，资源与仓库已更新");
                }
                else SetStatus(Model.LastError);
            });
            UiFactory.Panel("Separator", row, 0, 178, 1202, 1, UiFactory.Border);
        }
    }
}
