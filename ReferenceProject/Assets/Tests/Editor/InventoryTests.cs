using NUnit.Framework;

namespace HighPerfUI.Reference.Tests
{
    public sealed class InventoryTests
    {
        [Test] public void DatasetIsDeterministic()
        {
            var a = new InventoryModel(100); var b = new InventoryModel(100);
            Assert.That(a.Items.Count, Is.EqualTo(100));
            for (int i = 0; i < 100; i++)
            { Assert.That(a.Items[i].Id, Is.EqualTo(b.Items[i].Id)); Assert.That(a.Items[i].Power, Is.EqualTo(b.Items[i].Power)); }
        }
        [Test] public void EquipChangesOnlyMatchingSlot()
        {
            var model = new InventoryModel(100);
            Assert.That(model.Equip(8), Is.True);
            Assert.That(model.Equipped[model.Find(8).Kind], Is.EqualTo(8));
            Assert.That(model.Equip(-1), Is.False);
        }
        [Test] public void UpgradePreservesIdAndChangesPower()
        {
            var model = new InventoryModel(100); var item = model.Find(1);
            Assert.That(item, Is.Not.Null);
            int old = item.Power; int level = item.Level;
            Assert.That(model.Upgrade(1), Is.True);
            Assert.That(item.Id, Is.EqualTo(1)); Assert.That(item.Power, Is.GreaterThan(old));
            Assert.That(item.Level, Is.EqualTo(level + 1));
        }
    }
}
