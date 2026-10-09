using System;
using System.Collections.Generic;

namespace HighPerfUI.Reference
{
    [Serializable]
    public sealed class Equipment
    {
        public long Id;
        public string Name;
        public int Kind, Rarity, Level, Power;
        public bool Locked;
        public int SetId, Mastery;
        public long ExpiresAtUtc;
        public int Quantity = 1;

        internal Equipment Copy() => (Equipment)MemberwiseClone();
    }

    public sealed class RewardDefinition
    {
        public int Id;
        public string Title, Description;
        public int Gold, Ore;
        public int[] EquipmentKinds;
    }

    public static class RewardCatalog
    {
        public static readonly IReadOnlyList<RewardDefinition> Entries = Array.AsReadOnly(new[]
        {
            new RewardDefinition { Id = 1, Title = "前线补给", Description = "稀有武器、头盔与强化材料", Gold = 2500, Ore = 30,
                EquipmentKinds = new[] { 0, 1, 6 } },
            new RewardDefinition { Id = 2, Title = "远征嘉奖", Description = "守誓护甲、护手与英雄信物", Gold = 4200, Ore = 50,
                EquipmentKinds = new[] { 2, 3, 7 } },
            new RewardDefinition { Id = 3, Title = "限时军备", Description = "逐风战靴、饰品与限时补给", Gold = 1800, Ore = 20,
                EquipmentKinds = new[] { 4, 5, 8 } }
        });

        internal static RewardDefinition Find(int id)
        {
            foreach (var entry in Entries) if (entry.Id == id) return entry;
            return null;
        }
    }

    [Serializable]
    public sealed class InventoryUpgradeOperation
    {
        public string OperationId;
        public long ItemId;
    }

    [Serializable]
    public sealed class InventorySnapshot
    {
        public int SchemaVersion = 1;
        public int Gold, Ore, Revision;
        public long NextId;
        public List<Equipment> Items;
        public long[] Equipped;
        public int[] ClaimedRewards;
        public InventoryUpgradeOperation[] UpgradeOperations;
    }

    public sealed class InventoryModel
    {
        public readonly List<Equipment> Items = new List<Equipment>();
        public readonly long[] Equipped = new long[6];
        public int Gold = 25000, Ore = 250;
        public int Revision;
        public string LastError = string.Empty;
        public event Action<long[]> Changed;
        public static IReadOnlyList<RewardDefinition> RewardCatalog => HighPerfUI.Reference.RewardCatalog.Entries;
        public static readonly string[] KindNames = { "武器", "头盔", "护甲", "护手", "战靴", "饰品", "材料", "英雄信物", "限时补给" };
        private static readonly string[] Prefixes = { "苍银", "逐风", "赤焰", "寒星", "守誓", "暮光", "不朽", "黎明" };
        private static readonly int[] FixtureSets = { 0, 1, 1, 2, 2, 3 };
        private readonly HashSet<int> _claimedRewards = new HashSet<int>();
        private readonly Dictionary<string, long> _upgradeOperations = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly string _sessionId = Guid.NewGuid().ToString("N");
        private long _nextId;
        private long _legacyOperation;

        public InventoryModel(int count, int seed = 1729)
        {
            var random = new System.Random(seed);
            for (int i = 0; i < System.Math.Max(0, count); i++)
            {
                int kind = i % 6;
                Items.Add(new Equipment { Id = i + 1, Kind = kind, Rarity = random.Next(1, 5),
                    Level = random.Next(1, 16), Power = random.Next(80, 480), Locked = i % 11 == 0,
                    Name = Prefixes[i % Prefixes.Length] + KindNames[kind], SetId = FixtureSets[(i / 6) % FixtureSets.Length],
                    Mastery = kind == 0 || kind == 5 ? 20 + (i / 6) % 70 : 0,
                    ExpiresAtUtc = i >= 36 && (i / 6) % 12 == 6 ? 2000000000L : 0 });
            }
            for (int i = 0; i < 6 && i < count; i++) Equipped[i] = i + 1;
            _nextId = Math.Max(10001L, (long)Items.Count + 1);
        }

        public bool Equip(long id)
        {
            var item = Find(id);
            if (!IsEquipment(item)) return Fail("该物品不能穿戴");
            if (IsExpired(item)) return Fail("装备已过期");
            long previous = Equipped[item.Kind];
            if (previous == id) { LastError = string.Empty; return true; }
            if (!CanCommit()) return false;
            Equipped[item.Kind] = id;
            Commit(previous == 0 ? new[] { id } : new[] { previous, id });
            return true;
        }

        public bool Upgrade(long id)
        {
            return TryUpgrade(id, "legacy-" + _sessionId + "-" + (++_legacyOperation));
        }

        public int UpgradeCost(long id)
        {
            var item = Find(id);
            if (!IsEquipment(item) || item.Level < 1 || item.Level >= 30 || item.Rarity < 1 || item.Rarity > 5) return 0;
            return 100 + item.Level * 50 + item.Rarity * 75;
        }

        public int UpgradeOreCost(long id)
        {
            var item = Find(id);
            return UpgradeCost(id) == 0 ? 0 : item.Rarity;
        }

        public bool TryUpgrade(long id, string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId)) return Fail("强化操作编号不能为空");
            long recordedId;
            if (_upgradeOperations.TryGetValue(operationId, out recordedId))
            {
                if (recordedId != id) return Fail("强化操作编号已用于其他物品");
                LastError = string.Empty;
                return true;
            }
            var item = Find(id);
            int goldCost = UpgradeCost(id);
            if (goldCost == 0) return Fail(IsEquipment(item) && item.Level >= 30 ? "装备已达到强化上限" : "该物品不能强化");
            if (IsExpired(item)) return Fail("装备已过期");
            int oreCost = item.Rarity;
            if (Gold < goldCost || Ore < oreCost) return Fail("金币或矿石不足");
            int powerGain = 12 + item.Rarity * 3;
            if (item.Power < 0 || item.Power > int.MaxValue - powerGain) return Fail("装备战力超出有效范围");
            if (!CanCommit()) return false;
            _upgradeOperations.Add(operationId, id);
            Gold -= goldCost;
            Ore -= oreCost;
            item.Level++;
            item.Power += powerGain;
            Commit(new[] { id });
            return true;
        }

        public bool ToggleLock(long id)
        {
            var item = Find(id);
            if (item == null) return Fail("物品不存在");
            if (!CanCommit()) return false;
            item.Locked = !item.Locked;
            Commit(new[] { id });
            return true;
        }

        public bool IsRewardClaimed(int id) => _claimedRewards.Contains(id);

        public Equipment[] PreviewReward(int rewardId)
        {
            var reward = HighPerfUI.Reference.RewardCatalog.Find(rewardId);
            if (reward == null) return new Equipment[0];
            var result = new Equipment[reward.EquipmentKinds.Length];
            for (int i = 0; i < result.Length; i++)
            {
                int kind = reward.EquipmentKinds[i];
                bool equipment = kind < 6;
                result[i] = new Equipment
                {
                    Id = -((long)rewardId * 100 + i + 1), Kind = kind,
                    Name = equipment ? Prefixes[rewardId + 1] + KindNames[kind] : KindNames[kind],
                    Rarity = equipment || kind == 7 ? 4 : 2, Level = equipment ? 5 : 0,
                    Power = equipment ? 340 + kind * 23 + rewardId * 17 : 0,
                    SetId = equipment ? rewardId : 0, Mastery = equipment ? 35 + kind * 5 : kind == 7 ? 60 : 0,
                    ExpiresAtUtc = kind == 8 || equipment && rewardId == 3 ? 2000000000L : 0,
                    Quantity = equipment || kind == 8 ? 1 : kind == 7 ? 5 : 20
                };
            }
            return result;
        }

        public bool ClaimReward(int rewardId)
        {
            var reward = HighPerfUI.Reference.RewardCatalog.Find(rewardId);
            if (reward == null) return Fail("奖励不存在");
            if (_claimedRewards.Contains(rewardId)) return Fail("奖励已领取");
            if (reward.Gold < 0 || reward.Ore < 0 || Gold > int.MaxValue - reward.Gold || Ore > int.MaxValue - reward.Ore)
                return Fail("奖励资源超出有效范围");
            if (!CanCommit()) return false;
            long nextId = _nextId;
            foreach (var item in Items)
            {
                if (item == null || item.Id <= 0 || item.Id == long.MaxValue) return Fail("仓库物品编号无效");
                nextId = Math.Max(nextId, item.Id + 1);
            }
            var additions = PreviewReward(rewardId);
            if (nextId > long.MaxValue - additions.Length) return Fail("仓库物品编号已用尽");
            var ids = new long[additions.Length];
            for (int i = 0; i < additions.Length; i++)
            {
                additions[i].Id = nextId++;
                ids[i] = additions[i].Id;
            }
            if (Items.Capacity < Items.Count + additions.Length) Items.Capacity = Items.Count + additions.Length;
            _claimedRewards.Add(rewardId);
            Items.AddRange(additions);
            _nextId = nextId;
            Gold += reward.Gold;
            Ore += reward.Ore;
            Commit(ids);
            return true;
        }

        public Equipment Find(long id)
        {
            if (id <= 0) return null;
            // Keep the dense legacy fixture fast, but never confuse an index with an ID.
            if (id <= Items.Count)
            {
                var candidate = Items[(int)id - 1];
                if (candidate != null && candidate.Id == id) return candidate;
            }
            foreach (var item in Items) if (item != null && item.Id == id) return item;
            return null;
        }

        public int SetPieces
        {
            get
            {
                int largest = 0;
                var sets = new Dictionary<int, int>();
                foreach (long id in Equipped)
                {
                    var item = Find(id);
                    if (!IsEquipment(item) || item.SetId <= 0) continue;
                    int count;
                    sets.TryGetValue(item.SetId, out count);
                    sets[item.SetId] = ++count;
                    largest = Math.Max(largest, count);
                }
                return largest;
            }
        }
        public int TotalPower
        {
            get { int sum = 0; foreach (long id in Equipped) sum += Find(id)?.Power ?? 0; return sum; }
        }

        public int TotalDefense
        {
            get
            {
                long defense = 0;
                foreach (long id in Equipped) defense += (Find(id)?.Power ?? 0) / 5;
                if (SetPieces >= 2) defense = defense * 112 / 100;
                return (int)Math.Min(defense, int.MaxValue);
            }
        }

        public InventorySnapshot Capture()
        {
            var items = new List<Equipment>(Items.Count);
            long nextId = _nextId;
            foreach (var item in Items)
            {
                if (item == null || item.Id == long.MaxValue) throw new ArgumentException("Invalid inventory item.");
                items.Add(item.Copy());
                nextId = Math.Max(nextId, item.Id + 1);
            }
            var claims = new int[_claimedRewards.Count];
            _claimedRewards.CopyTo(claims);
            Array.Sort(claims);
            var operationIds = new List<string>(_upgradeOperations.Keys);
            operationIds.Sort(StringComparer.Ordinal);
            var operations = new InventoryUpgradeOperation[operationIds.Count];
            for (int i = 0; i < operations.Length; i++)
                operations[i] = new InventoryUpgradeOperation { OperationId = operationIds[i], ItemId = _upgradeOperations[operationIds[i]] };
            return new InventorySnapshot { Gold = Gold, Ore = Ore, Revision = Revision, NextId = nextId,
                Items = items, Equipped = (long[])Equipped.Clone(), ClaimedRewards = claims, UpgradeOperations = operations };
        }

        public static InventoryModel Restore(InventorySnapshot snapshot)
        {
            Validate(snapshot);
            var model = new InventoryModel(0) { Gold = snapshot.Gold, Ore = snapshot.Ore, Revision = snapshot.Revision, _nextId = snapshot.NextId };
            foreach (var item in snapshot.Items) model.Items.Add(item.Copy());
            Array.Copy(snapshot.Equipped, model.Equipped, 6);
            foreach (int reward in snapshot.ClaimedRewards) model._claimedRewards.Add(reward);
            foreach (var operation in snapshot.UpgradeOperations) model._upgradeOperations.Add(operation.OperationId, operation.ItemId);
            return model;
        }

        internal static void Validate(InventorySnapshot snapshot)
        {
            if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.Gold < 0 || snapshot.Ore < 0 || snapshot.Revision < 0
                || snapshot.Items == null || snapshot.Items.Count > 100000 || snapshot.Equipped == null || snapshot.Equipped.Length != 6
                || snapshot.ClaimedRewards == null || snapshot.UpgradeOperations == null || snapshot.NextId <= 0)
                throw new ArgumentException("Invalid or unsupported inventory snapshot.");
            var items = new Dictionary<long, Equipment>();
            foreach (var item in snapshot.Items)
            {
                if (item == null || item.Id <= 0 || item.Id >= snapshot.NextId || items.ContainsKey(item.Id)
                    || string.IsNullOrWhiteSpace(item.Name) || item.Kind < 0 || item.Kind > 8 || item.Rarity < 1 || item.Rarity > 5
                    || item.Level < (item.Kind < 6 ? 1 : 0) || item.Level > 30 || item.Power < 0
                    || item.SetId < 0 || item.Mastery < 0 || item.Mastery > 100 || item.ExpiresAtUtc < 0 || item.Quantity <= 0)
                    throw new ArgumentException("Invalid equipment or duplicate stable ID.");
                items.Add(item.Id, item);
            }
            for (int slot = 0; slot < 6; slot++)
            {
                long id = snapshot.Equipped[slot];
                Equipment item;
                if (id != 0 && (!items.TryGetValue(id, out item) || item.Kind != slot))
                    throw new ArgumentException("Equipped ID does not match its equipment slot.");
            }
            var rewards = new HashSet<int>();
            foreach (int reward in snapshot.ClaimedRewards)
                if (HighPerfUI.Reference.RewardCatalog.Find(reward) == null || !rewards.Add(reward))
                    throw new ArgumentException("Invalid or duplicate reward record.");
            var operations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var operation in snapshot.UpgradeOperations)
            {
                Equipment item;
                if (operation == null || string.IsNullOrWhiteSpace(operation.OperationId) || !operations.Add(operation.OperationId)
                    || !items.TryGetValue(operation.ItemId, out item) || !IsEquipment(item))
                    throw new ArgumentException("Invalid or duplicate upgrade operation.");
            }
        }

        private static bool IsEquipment(Equipment item) => item != null && item.Kind >= 0 && item.Kind < 6;
        private static bool IsExpired(Equipment item) => item.ExpiresAtUtc > 0 && item.ExpiresAtUtc <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private bool CanCommit()
        {
            return Gold < 0 || Ore < 0 || Revision < 0 || Revision == int.MaxValue ? Fail("仓库资源或版本无效") : true;
        }

        private bool Fail(string error) { LastError = error; return false; }

        private void Commit(long[] ids)
        {
            Revision++;
            LastError = string.Empty;
            Changed?.Invoke(ids);
        }
    }
}
