using NUnit.Framework;

namespace HighPerfUI.Reference.Tests
{
    public sealed class BenchmarkMathTests
    {
        [Test] public void EmptyPercentileIsUnavailableNotZero()
        { Assert.That(BenchmarkRunner.Percentile(new double[0], .95), Is.EqualTo(-1)); }
        [Test] public void PercentileUsesNearestRank()
        { Assert.That(BenchmarkRunner.Percentile(new double[] { 1, 2, 3, 4, 100 }, .95), Is.EqualTo(100)); }
        [Test] public void DifferentEquipmentChangesSetProgress()
        {
            var model = new InventoryModel(100);
            var property = typeof(InventoryModel).GetProperty("SetPieces");
            Assert.That(property, Is.Not.Null);
            Assert.That((int)property.GetValue(model), Is.Zero);
            model.Equip(7); model.Equip(14);
            Assert.That((int)property.GetValue(model), Is.EqualTo(2));
        }
    }
}
