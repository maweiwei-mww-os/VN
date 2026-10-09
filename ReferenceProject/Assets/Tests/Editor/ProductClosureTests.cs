using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HighPerfUI.Reference.Tests
{
    public sealed class ProductClosureTests
    {
        [Test]
        public void RecoveryQuarantinesCorruptionAndAllowsFurtherSaving()
        {
            string directory = Path.Combine(Path.GetTempPath(), "armory-recovery-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "save.json");
            try
            {
                var model = new InventoryModel(12);
                InventorySaveStore.Save(path, model);
                model.Upgrade(1); InventorySaveStore.Save(path, model);
                const string corrupt = "broken primary evidence"; File.WriteAllText(path, corrupt);
                var recovered = InventorySaveStore.Load(path, out var warning);
                Assert.That(recovered, Is.Not.Null); Assert.That(warning, Is.Not.Empty);
                var method = typeof(InventorySaveStore).GetMethod("RecoverPrimary", BindingFlags.Static | BindingFlags.Public);
                Assert.That(method, Is.Not.Null);
                string quarantine = (string)method.Invoke(null, new object[] { path });
                Assert.That(File.ReadAllText(quarantine), Is.EqualTo(corrupt));
                recovered.Upgrade(2); InventorySaveStore.Save(path, recovered);
                var restarted = InventorySaveStore.Load(path, out warning);
                Assert.That(warning, Is.Empty); Assert.That(restarted.Gold, Is.EqualTo(recovered.Gold));
                Assert.That(restarted.Find(2).Level, Is.EqualTo(recovered.Find(2).Level));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void MasteryPercentageChangesActualGeometry()
        {
            var root = new GameObject("mastery-geometry-test");
            try
            {
                var view = EquipmentFragmentPresentation.Create(4, root.transform).GetComponent<EquipmentFragmentPresentation>();
                view.Apply(new Equipment { Mastery = 25 });
                Assert.That(view.Progress.rectTransform.rect.width, Is.EqualTo(21).Within(.01));
                view.Apply(new Equipment { Mastery = 80 });
                Assert.That(view.Progress.rectTransform.rect.width, Is.EqualTo(67.2).Within(.01));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
