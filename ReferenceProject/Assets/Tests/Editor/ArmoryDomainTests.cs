using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace HighPerfUI.Reference.Tests
{
    public sealed class ArmoryDomainTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "HighPerfUI-Armory-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void LegacyThousandItemFixtureRemainsDeterministicAndEquippable()
        {
            var a = new InventoryModel(1000);
            var b = new InventoryModel(1000);
            Assert.That(a.Items.Count, Is.EqualTo(1000));
            Assert.That(a.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(1000));
            for (int i = 0; i < a.Items.Count; i++)
            {
                Assert.That(a.Items[i].Kind, Is.InRange(0, 5));
                Assert.That(a.Items[i].Id, Is.EqualTo(b.Items[i].Id));
                Assert.That(a.Items[i].Name, Is.EqualTo(b.Items[i].Name));
                Assert.That(a.Items[i].Power, Is.EqualTo(b.Items[i].Power));
                Assert.That(a.Equip(a.Items[i].Id), Is.True);
            }
        }

        [Test]
        public void FindUsesActualStableIdsAfterInsertionRemovalAndReorder()
        {
            var model = new InventoryModel(0);
            var first = Item(41, 0);
            var second = Item(9007, 1);
            model.Items.Add(first);
            model.Items.Add(second);
            Assert.That(model.Find(41), Is.SameAs(first));
            Assert.That(model.Find(9007), Is.SameAs(second));
            Assert.That(model.Find(1), Is.Null);
            model.Items.Reverse();
            Assert.That(model.Find(41), Is.SameAs(first));
            Assert.That(model.Equip(9007), Is.True);
            Assert.That(model.Equipped[1], Is.EqualTo(9007));
            model.Items.Remove(first);
            Assert.That(model.Find(41), Is.Null);
            Assert.That(model.TotalPower, Is.EqualTo(second.Power));
        }

        [Test]
        public void FindRejectsRemovedIdsAfterEqualCountReplacement()
        {
            var model = new InventoryModel(3);
            var removed = model.Find(2);
            Assert.That(removed, Is.SameAs(model.Items[1]));
            int count = model.Items.Count;
            model.Items.Remove(removed);
            var replacement = Item(9007, 1);
            model.Items.Insert(1, replacement);
            Assert.That(model.Items.Count, Is.EqualTo(count));
            Assert.That(model.Find(2), Is.Null);
            Assert.That(model.Find(9007), Is.SameAs(replacement));
            Assert.That(model.Equip(9007), Is.True);
            Assert.That(model.Equipped[1], Is.EqualTo(9007));

            var sameIdReplacement = Item(9007, 1);
            model.Items[1] = sameIdReplacement;
            Assert.That(model.Find(9007), Is.SameAs(sameIdReplacement));
            Assert.That(model.Find(9007), Is.Not.SameAs(replacement));
        }

        [Test]
        public void TotalDefenseUsesPerItemIntegerContributionAndIgnoresMissingSlots()
        {
            var model = new InventoryModel(0);
            Assert.That(Number(model, "TotalDefense"), Is.Zero);
            var first = Item(41, 0);
            var second = Item(9007, 1);
            first.Power = 104;
            second.Power = 109;
            model.Items.AddRange(new[] { first, second });
            Assert.That(model.Equip(first.Id), Is.True);
            Assert.That(model.Equip(second.Id), Is.True);
            model.Equipped[2] = 99999;
            Assert.That(Number(model, "TotalDefense"), Is.EqualTo(41));
            Assert.That(model.TotalPower, Is.EqualTo(213));
        }

        [Test]
        public void TotalDefenseBonusRequiresTwoEquippedPiecesOfTheSameExplicitSet()
        {
            var model = new InventoryModel(0);
            var first = Item(41, 0);
            var second = Item(9007, 1);
            first.Power = 104;
            second.Power = 109;
            Put(first, "SetId", 3);
            Put(second, "SetId", 4);
            var unequipped = Item(10001, 2);
            Put(unequipped, "SetId", 3);
            model.Items.AddRange(new[] { first, second, unequipped });
            Assert.That(model.Equip(first.Id), Is.True);
            Assert.That(model.Equip(second.Id), Is.True);
            Assert.That(Number(model, "TotalDefense"), Is.EqualTo(41));
            Put(second, "SetId", 3);
            Assert.That(Number(model, "TotalDefense"), Is.EqualTo(45));
            Assert.That(model.TotalPower, Is.EqualTo(213), "The defense set bonus must not alter legacy raw power.");
            Assert.That(Call<bool>(model, "TryUpgrade", first.Id, "defense-upgrade"), Is.True);
            Assert.That(Number(model, "TotalDefense"), Is.EqualTo(50));
            Assert.That(model.TotalPower, Is.EqualTo(231));
            Put(second, "SetId", 4);
            Assert.That(Number(model, "TotalDefense"), Is.EqualTo(45));
        }

        [Test]
        public void TotalDefenseDoesNotOverflowForHighPowerEquipment()
        {
            var model = new InventoryModel(6);
            foreach (var item in model.Items)
            {
                item.Power = int.MaxValue;
                Put(item, "SetId", 3);
            }
            Assert.That(Number(model, "TotalDefense"), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ExplicitSetMetadataCountsMatchingPiecesWithoutIdModulo()
        {
            var model = new InventoryModel(0);
            var first = Item(1, 0);
            var second = Item(2, 1);
            var unset = Item(7, 2);
            Put(first, "SetId", 3);
            Put(second, "SetId", 3);
            Put(unset, "SetId", 0);
            model.Items.AddRange(new[] { first, second, unset });
            Assert.That(model.Equip(1), Is.True);
            Assert.That(model.Equip(2), Is.True);
            Assert.That(model.Equip(7), Is.True);
            Assert.That(model.SetPieces, Is.EqualTo(2));
            Put(second, "SetId", 4);
            Assert.That(model.SetPieces, Is.EqualTo(1));
        }

        [Test]
        public void UpgradeDebitsQuotedGoldAndOreAndPublishesCommittedItemOnce()
        {
            var model = FundedModel();
            var item = model.Find(1);
            item.Level = 2;
            item.Rarity = 3;
            int cost = Call<int>(model, "UpgradeCost", item.Id);
            Assert.That(cost, Is.GreaterThan(0));
            Assert.That(Call<int>(model, "UpgradeOreCost", item.Id), Is.EqualTo(3));
            int gold = Number(model, "Gold");
            int ore = Number(model, "Ore");
            int revision = Number(model, "Revision");
            int power = item.Power;
            var changes = Watch(model, ids =>
            {
                Assert.That(item.Level, Is.EqualTo(3));
                Assert.That(Number(model, "Gold"), Is.EqualTo(gold - cost));
                Assert.That(Number(model, "Ore"), Is.EqualTo(ore - 3));
                Assert.That(Number(model, "Revision"), Is.EqualTo(revision + 1));
            });
            Assert.That(Call<bool>(model, "TryUpgrade", item.Id, "upgrade-1"), Is.True);
            Assert.That(item.Power, Is.EqualTo(power + 21));
            Assert.That(item.Id, Is.EqualTo(1));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0], Is.EquivalentTo(new[] { item.Id }));
            Assert.That(Call<int>(model, "UpgradeCost", item.Id), Is.GreaterThan(cost));
        }

        [TestCase("Gold")]
        [TestCase("Ore")]
        public void InsufficientResourceDoesNotMutateAnyBusinessState(string resource)
        {
            var model = FundedModel();
            Put(model, resource, 0);
            string before = State(model);
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "insufficient"), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(changes, Is.Empty);
            Assert.That(Convert.ToString(Read(model, "LastError")), Is.Not.Empty);
            Put(model, resource, 1000000);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "insufficient"), Is.True,
                "A failed attempt must not consume the operation ID.");
        }

        [Test]
        public void SuccessfulOperationIdIsIdempotentAndCannotUpgradeAnotherItem()
        {
            var model = FundedModel();
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "one-confirmation"), Is.True);
            string committed = State(model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "one-confirmation"), Is.True);
            Assert.That(State(model), Is.EqualTo(committed));
            Assert.That(Call<bool>(model, "TryUpgrade", 2L, "one-confirmation"), Is.False);
            Assert.That(State(model), Is.EqualTo(committed));
            Assert.That(changes.Count, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidOperationIdDoesNotSpendOrUpgrade(string operationId)
        {
            var model = FundedModel();
            string before = State(model);
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, operationId), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void MaxLevelAndMissingItemRejectWithoutResourceOrRevisionChanges()
        {
            var model = FundedModel();
            model.Find(1).Level = 30;
            string before = State(model);
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "max"), Is.False);
            Assert.That(Call<bool>(model, "TryUpgrade", 9999999L, "missing"), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void LegacyUpgradeUsesFreshOperationsButStillConsumesResources()
        {
            var model = FundedModel();
            var item = model.Find(1);
            item.Level = 1;
            int gold = Number(model, "Gold");
            Assert.That(model.Upgrade(1), Is.True);
            Assert.That(model.Upgrade(1), Is.True);
            Assert.That(item.Level, Is.EqualTo(3));
            Assert.That(Number(model, "Gold"), Is.LessThan(gold));
            Put(model, "Gold", 0);
            string before = State(model);
            Assert.That(model.Upgrade(1), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
        }

        [Test]
        public void LockTogglePublishesOnlyTheChangedItemAndMissingIdDoesNothing()
        {
            var model = FundedModel();
            bool locked = model.Find(1).Locked;
            int revision = Number(model, "Revision");
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "ToggleLock", 1L), Is.True);
            Assert.That(model.Find(1).Locked, Is.EqualTo(!locked));
            Assert.That(Number(model, "Revision"), Is.EqualTo(revision + 1));
            Assert.That(changes.Single(), Is.EquivalentTo(new[] { 1L }));
            string before = State(model);
            Assert.That(Call<bool>(model, "ToggleLock", -1L), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void EquipReplacementPublishesBothStableIdsWithoutSpending()
        {
            var model = FundedModel();
            int gold = Number(model, "Gold");
            int ore = Number(model, "Ore");
            int revision = Number(model, "Revision");
            var changes = Watch(model);
            Assert.That(model.Equip(7), Is.True);
            Assert.That(model.Equipped[0], Is.EqualTo(7));
            Assert.That(changes.Single(), Is.EquivalentTo(new[] { 1L, 7L }));
            Assert.That(Number(model, "Revision"), Is.EqualTo(revision + 1));
            Assert.That(Number(model, "Gold"), Is.EqualTo(gold));
            Assert.That(Number(model, "Ore"), Is.EqualTo(ore));
            Assert.That(model.Equip(7), Is.True);
            Assert.That(changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void RewardCatalogContainsMixedKindsAndClaimsExactlyTheAdvertisedGrant()
        {
            var model = FundedModel();
            var rewards = Rewards();
            var allKinds = rewards.SelectMany(reward => Kinds(reward)).Distinct().OrderBy(kind => kind).ToArray();
            Assert.That(allKinds, Is.EqualTo(Enumerable.Range(0, 9).ToArray()));
            var reward = rewards.First(entry => Kinds(entry).Length > 0);
            int id = Number(reward, "Id");
            Assert.That(Convert.ToString(Read(reward, "Title")), Is.Not.Empty);
            Assert.That(Convert.ToString(Read(reward, "Description")), Is.Not.Empty);
            int gold = Number(model, "Gold");
            int ore = Number(model, "Ore");
            int revision = Number(model, "Revision");
            long[] oldIds = model.Items.Select(item => item.Id).ToArray();
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "IsRewardClaimed", id), Is.False);
            Assert.That(Call<bool>(model, "ClaimReward", id), Is.True);
            var added = model.Items.Where(item => !oldIds.Contains(item.Id)).ToArray();
            Assert.That(added.Select(item => item.Kind), Is.EquivalentTo(Kinds(reward)));
            Assert.That(Number(model, "Gold"), Is.EqualTo(gold + Number(reward, "Gold")));
            Assert.That(Number(model, "Ore"), Is.EqualTo(ore + Number(reward, "Ore")));
            Assert.That(Number(model, "Revision"), Is.EqualTo(revision + 1));
            Assert.That(Call<bool>(model, "IsRewardClaimed", id), Is.True);
            Assert.That(model.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(model.Items.Count));
            Assert.That(added.All(item => item.Id > 0 && model.Find(item.Id) == item), Is.True);
            Assert.That(changes.Single(), Is.EquivalentTo(added.Select(item => item.Id)));
            string committed = State(model);
            Assert.That(Call<bool>(model, "ClaimReward", id), Is.False);
            Assert.That(State(model), Is.EqualTo(committed));
            Assert.That(changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnknownRewardAndWalletOverflowDoNotPartiallyGrant()
        {
            var model = FundedModel();
            string before = State(model);
            var changes = Watch(model);
            Assert.That(Call<bool>(model, "ClaimReward", int.MinValue), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            var reward = Rewards().First(entry => Number(entry, "Gold") > 0);
            int id = Number(reward, "Id");
            Put(model, "Gold", int.MaxValue);
            before = State(model);
            Assert.That(Call<bool>(model, "ClaimReward", id), Is.False);
            Assert.That(State(model), Is.EqualTo(before));
            Assert.That(Call<bool>(model, "IsRewardClaimed", id), Is.False);
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void MixedNonEquipmentRewardsCannotBeEquippedOrUpgraded()
        {
            var model = FundedModel();
            foreach (object reward in Rewards()) Assert.That(Call<bool>(model, "ClaimReward", Number(reward, "Id")), Is.True);
            var mixed = model.Items.Where(item => item.Kind >= 6).ToArray();
            Assert.That(mixed.Select(item => item.Kind).Distinct(), Is.EquivalentTo(new[] { 6, 7, 8 }));
            foreach (var item in mixed)
            {
                string before = State(model);
                Assert.That(model.Equip(item.Id), Is.False);
                Assert.That(Call<bool>(model, "TryUpgrade", item.Id, "mixed-" + item.Id), Is.False);
                Assert.That(State(model), Is.EqualTo(before));
            }
        }

        [Test]
        public void RewardPreviewsAreDetachedAndExactlyMatchGrantedMetadata()
        {
            var model = new InventoryModel(12);
            Assert.That(Call<Equipment[]>(model, "PreviewReward", int.MinValue), Is.Empty);
            foreach (object reward in Rewards())
            {
                int id = Number(reward, "Id");
                var preview = Call<Equipment[]>(model, "PreviewReward", id);
                Assert.That(preview.Select(item => item.Kind), Is.EquivalentTo(Kinds(reward)));
                Assert.That(preview.All(item => item.Id < 0), Is.True);
                var mutated = Call<Equipment[]>(model, "PreviewReward", id);
                mutated[0].Name = "Must not reach the grant";
                mutated[0].Power += 1234;
                int count = model.Items.Count;
                Assert.That(Call<bool>(model, "ClaimReward", id), Is.True);
                var granted = model.Items.Skip(count).ToArray();
                Assert.That(granted.Length, Is.EqualTo(preview.Length));
                for (int i = 0; i < preview.Length; i++)
                {
                    Assert.That(granted[i], Is.Not.SameAs(preview[i]));
                    Assert.That(granted[i].Id, Is.GreaterThan(0));
                    foreach (FieldInfo field in typeof(Equipment).GetFields(BindingFlags.Public | BindingFlags.Instance))
                        if (field.Name != "Id") Assert.That(field.GetValue(granted[i]), Is.EqualTo(field.GetValue(preview[i])), field.Name);
                }
            }
        }

        [Test]
        public void CaptureAndRestoreAreDeepCopiesOfAllEquipmentMetadata()
        {
            var model = FundedModel();
            var item = model.Find(1);
            Put(item, "SetId", 2);
            Put(item, "Mastery", 37);
            Put(item, "ExpiresAtUtc", 2000000000L);
            Put(item, "Quantity", 4);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "snapshot-operation"), Is.True);
            int rewardId = Number(Rewards()[0], "Id");
            Assert.That(Call<bool>(model, "ClaimReward", rewardId), Is.True);
            Assert.That(Call<bool>(model, "ToggleLock", 1L), Is.True);
            Assert.That(model.Equip(7), Is.True);
            string expected = State(model);
            object snapshot = Call<object>(model, "Capture");
            model.Find(1).Power += 999;
            model.Equipped[0] = 1;
            Put(model, "Gold", 0);
            var restored = Restore(snapshot);
            Assert.That(State(restored), Is.EqualTo(expected));
            Assert.That(Call<bool>(restored, "IsRewardClaimed", rewardId), Is.True);
            Assert.That(Call<bool>(restored, "TryUpgrade", 1L, "snapshot-operation"), Is.True);
            Assert.That(State(restored), Is.EqualTo(expected));
            restored.Find(1).Power += 111;
            Assert.That(State(Restore(snapshot)), Is.EqualTo(expected));
        }

        [Test]
        public void RestoreRejectsDuplicateStableIdsInsteadOfAliasingTwoItems()
        {
            object snapshot = Call<object>(FundedModel(), "Capture");
            var items = Read(snapshot, "Items") as IList;
            Assert.That(items, Is.Not.Null, "Snapshot Items must expose the serialized item collection.");
            Put(items[1], "Id", Convert.ToInt64(Read(items[0], "Id")));
            Assert.Throws<ArgumentException>(() => Restore(snapshot));
        }

        [Test]
        public void SaveRoundTripPreservesWalletEquipmentRewardAndOperationRecords()
        {
            var model = FundedModel();
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "saved-operation"), Is.True);
            int rewardId = Number(Rewards()[0], "Id");
            Assert.That(Call<bool>(model, "ClaimReward", rewardId), Is.True);
            Assert.That(Call<bool>(model, "ToggleLock", 1L), Is.True);
            Assert.That(model.Equip(7), Is.True);
            string expected = State(model);
            string path = Path.Combine(directory, "inventory.json");
            Save(path, model);
            Assert.That(File.Exists(path), Is.True);
            string warning;
            var loaded = Load(path, out warning);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(warning, Is.Null.Or.Empty);
            Assert.That(State(loaded), Is.EqualTo(expected));
            Assert.That(Call<bool>(loaded, "IsRewardClaimed", rewardId), Is.True);
            Assert.That(Call<bool>(loaded, "ClaimReward", rewardId), Is.False);
            Assert.That(Call<bool>(loaded, "TryUpgrade", 1L, "saved-operation"), Is.True);
            Assert.That(State(loaded), Is.EqualTo(expected));
        }

        [Test]
        public void CorruptPrimaryRecoversPreviousBackupWithWarningWithoutOverwritingEitherFile()
        {
            var model = FundedModel();
            string path = Path.Combine(directory, "inventory.json");
            string expectedBackup = State(model);
            Save(path, model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "newer-save"), Is.True);
            Save(path, model);
            Assert.That(File.Exists(path + ".bak"), Is.True);
            byte[] backup = File.ReadAllBytes(path + ".bak");
            File.WriteAllText(path, "{broken save", Encoding.UTF8);
            byte[] corrupted = File.ReadAllBytes(path);
            string warning;
            var recovered = Load(path, out warning);
            Assert.That(recovered, Is.Not.Null);
            Assert.That(State(recovered), Is.EqualTo(expectedBackup));
            Assert.That(warning, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(corrupted));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }

        [Test]
        public void ChecksumRejectsSyntacticallyValidTamperingAndUsesBackup()
        {
            var model = FundedModel();
            model.Find(1).Name = "ChecksumOriginal";
            string path = Path.Combine(directory, "inventory.json");
            Save(path, model);
            string backupState = State(model);
            Assert.That(Call<bool>(model, "TryUpgrade", 1L, "tamper-save"), Is.True);
            Save(path, model);
            string json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("ChecksumOriginal"));
            File.WriteAllText(path, json.Replace("ChecksumOriginal", "ChecksumTampered"), Encoding.UTF8);
            string warning;
            var loaded = Load(path, out warning);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(State(loaded), Is.EqualTo(backupState));
            Assert.That(warning, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void UnrecoverableSaveReturnsVisibleFailureWithoutCreatingFreshState()
        {
            string path = Path.Combine(directory, "inventory.json");
            File.WriteAllText(path, "broken-primary");
            File.WriteAllText(path + ".bak", "broken-backup");
            string warning;
            Assert.That(Load(path, out warning), Is.Null);
            Assert.That(warning, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo("broken-primary"));
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo("broken-backup"));
        }

        [Test]
        public void InvalidStateSaveCannotReplaceLastGoodPrimaryOrBackup()
        {
            var model = FundedModel();
            string path = Path.Combine(directory, "inventory.json");
            Save(path, model);
            Save(path, model);
            byte[] primary = File.ReadAllBytes(path);
            byte[] backup = File.ReadAllBytes(path + ".bak");
            Put(model, "Gold", -1);
            Assert.Throws<ArgumentException>(() => Save(path, model));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }

        [Test]
        public void SavingOverCorruptPrimaryDoesNotSilentlyDestroyRecoveryEvidence()
        {
            var model = FundedModel();
            string path = Path.Combine(directory, "inventory.json");
            Save(path, model);
            Save(path, model);
            byte[] backup = File.ReadAllBytes(path + ".bak");
            File.WriteAllText(path, "corrupt-evidence");
            Assert.Throws<InvalidDataException>(() => Save(path, model));
            Assert.That(File.ReadAllText(path), Is.EqualTo("corrupt-evidence"));
            Assert.That(File.ReadAllBytes(path + ".bak"), Is.EqualTo(backup));
        }

        private static Equipment Item(long id, int kind)
        {
            return new Equipment { Id = id, Name = "Fixture", Kind = kind, Rarity = 2, Level = 1, Power = 100 };
        }

        private static InventoryModel FundedModel()
        {
            var model = new InventoryModel(12);
            Put(model, "Gold", 1000000);
            Put(model, "Ore", 1000000);
            return model;
        }

        private static int Number(object target, string name)
        {
            return Convert.ToInt32(Read(target, name));
        }

        // Resolve new contracts at runtime so the RED suite compiles against the old model.
        private static MemberInfo Member(Type type, string name, bool isStatic = false)
        {
            BindingFlags flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            MemberInfo member = (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
            Assert.That(member, Is.Not.Null, type.FullName + " must expose " + name + ".");
            return member;
        }

        private static object Read(object target, string name)
        {
            MemberInfo member = Member(target.GetType(), name);
            var field = member as FieldInfo;
            return field != null ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target, null);
        }

        private static void Put(object target, string name, object value)
        {
            MemberInfo member = Member(target.GetType(), name);
            var field = member as FieldInfo;
            Type type = field != null ? field.FieldType : ((PropertyInfo)member).PropertyType;
            object converted = value == null || type.IsInstanceOfType(value) ? value : Convert.ChangeType(value, type);
            if (field != null) field.SetValue(target, converted);
            else ((PropertyInfo)member).SetValue(target, converted, null);
        }

        private static MethodInfo Method(Type type, string name, Type[] parameters, bool isStatic = false)
        {
            var method = type.GetMethod(name, BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance),
                null, parameters, null);
            Assert.That(method, Is.Not.Null, type.FullName + " must expose " + name + " with the agreed parameters.");
            return method;
        }

        private static object Invoke(MethodInfo method, object target, object[] arguments)
        {
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null) throw exception.InnerException;
                throw;
            }
        }

        private static T Call<T>(object target, string name, params object[] arguments)
        {
            Type[] parameters = arguments.Select(value => value == null ? typeof(string) : value.GetType()).ToArray();
            return (T)Invoke(Method(target.GetType(), name, parameters), target, arguments);
        }

        private static List<long[]> Watch(InventoryModel model, Action<long[]> onChange = null)
        {
            var changed = typeof(InventoryModel).GetEvent("Changed", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(changed, Is.Not.Null, "InventoryModel must expose Changed with stable item IDs.");
            Assert.That(changed.EventHandlerType, Is.EqualTo(typeof(Action<long[]>)));
            var records = new List<long[]>();
            Action<long[]> handler = ids =>
            {
                Assert.That(ids, Is.Not.Null);
                records.Add((long[])ids.Clone());
                if (onChange != null) onChange(ids);
            };
            changed.AddEventHandler(model, handler);
            return records;
        }

        private static object[] Rewards()
        {
            MemberInfo member = Member(typeof(InventoryModel), "RewardCatalog", true);
            var field = member as FieldInfo;
            object value = field != null ? field.GetValue(null) : ((PropertyInfo)member).GetValue(null, null);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            object[] rewards = ((IEnumerable)value).Cast<object>().ToArray();
            Assert.That(rewards, Is.Not.Empty);
            Assert.That(rewards.Select(reward => Number(reward, "Id")).Distinct().Count(), Is.EqualTo(rewards.Length));
            return rewards;
        }

        private static int[] Kinds(object reward)
        {
            var kinds = Read(reward, "EquipmentKinds") as IEnumerable;
            Assert.That(kinds, Is.Not.Null);
            return kinds.Cast<object>().Select(Convert.ToInt32).ToArray();
        }

        private static InventoryModel Restore(object snapshot)
        {
            Assert.That(snapshot, Is.Not.Null);
            return (InventoryModel)Invoke(Method(typeof(InventoryModel), "Restore", new[] { snapshot.GetType() }, true),
                null, new[] { snapshot });
        }

        private static Type StoreType()
        {
            var type = typeof(InventoryModel).Assembly.GetType("HighPerfUI.Reference.InventorySaveStore");
            Assert.That(type, Is.Not.Null, "InventorySaveStore must provide versioned, checked persistence.");
            return type;
        }

        private static void Save(string path, InventoryModel model)
        {
            Invoke(Method(StoreType(), "Save", new[] { typeof(string), typeof(InventoryModel) }, true),
                null, new object[] { path, model });
        }

        private static InventoryModel Load(string path, out string warning)
        {
            object[] arguments = { path, null };
            var result = (InventoryModel)Invoke(Method(StoreType(), "Load", new[] { typeof(string), typeof(string).MakeByRefType() }, true),
                null, arguments);
            warning = (string)arguments[1];
            return result;
        }

        private static string State(InventoryModel model)
        {
            var state = new StringBuilder();
            state.Append(Number(model, "Gold")).Append('|').Append(Number(model, "Ore")).Append('|')
                .Append(Number(model, "Revision")).Append('|').Append(string.Join(",", model.Equipped));
            foreach (var item in model.Items)
            {
                state.Append(';').Append(item.Id).Append('|').Append(item.Name).Append('|').Append(item.Kind)
                    .Append('|').Append(item.Rarity).Append('|').Append(item.Level).Append('|').Append(item.Power)
                    .Append('|').Append(item.Locked).Append('|').Append(Read(item, "SetId"))
                    .Append('|').Append(Read(item, "Mastery")).Append('|').Append(Read(item, "ExpiresAtUtc"))
                    .Append('|').Append(Read(item, "Quantity"));
            }
            return state.ToString();
        }
    }
}
