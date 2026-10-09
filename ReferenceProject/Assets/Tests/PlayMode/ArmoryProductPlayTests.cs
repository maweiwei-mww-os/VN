using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HighPerfUI.Reference.Tests
{
    public sealed class ArmoryProductPlayTests
    {
        [UnityTest]
        public IEnumerator RewardCenterCanBeOpenedAndClosedByActualPointer()
        {
            var root = new GameObject("armory-product-test");
            var screen = root.AddComponent<InventoryScreen>();
            try
            {
                for (int i = 0; i < 180; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (i > 20 && screen.Ready) break; }
                Click(Find(root, "奖励中心"));
                yield return null;
                Assert.That(Find(root, "领取奖励"), Is.Not.Null);
                int count = screen.Model.Items.Count;
                Click(Find(root, "领取奖励"));
                for (int i = 0; i < 12; i++) yield return null;
                Assert.That(screen.Model.Items.Count, Is.GreaterThan(count), "Rewards must enter the actual inventory.");
                Click(Find(root, "返回军备库"));
                for (int i = 0; i < 45; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (screen.Ready) break; }
                Assert.That(screen.Ready, Is.True);
                Assert.That(root.GetComponent<BenchmarkRunner>().ValidateVisible(), Is.True);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DiagnosticsAreOptInAndLockedFilterWorks()
        {
            var root = new GameObject("armory-product-test");
            var screen = root.AddComponent<InventoryScreen>();
            try
            {
                for (int i = 0; i < 180; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (i > 20 && screen.Ready) break; }
                Assert.That(root.transform.Find("Inventory Canvas/Inventory/Diagnostics"), Is.Not.Null);
                Assert.That(root.transform.Find("Inventory Canvas/Inventory/Diagnostics").gameObject.activeSelf, Is.False);
                Click(Find(root, "仅锁定"));
                for (int i = 0; i < 45; i++) yield return null;
                Assert.That(screen.Count, Is.GreaterThan(0));
                Assert.That(screen.Count, Is.LessThan(screen.Model.Items.Count));
                for (int i = 0; i < screen.Count; i++) Assert.That(screen.Model.Find(screen.GetStableId(i)).Locked, Is.True);
                Click(Find(root, "性能对照"));
                yield return null;
                Assert.That(root.transform.Find("Inventory Canvas/Inventory/Diagnostics").gameObject.activeSelf, Is.True);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SortedInventoryRefreshesChangedItemContent()
        {
            var root = new GameObject("armory-sorted-update-test");
            var screen = root.AddComponent<InventoryScreen>();
            try
            {
                for (int i = 0; i < 180; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (i > 20 && screen.Ready) break; }
                Click(Find(root, "按战力"));
                for (int i = 0; i < 45; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (i > 5 && screen.Ready) break; }
                long id = screen.GetStableId(0);
                Assert.That(screen.Model.TryUpgrade(id, "sorted-upgrade-regression"), Is.True, screen.Model.LastError);
                for (int i = 0; i < 45; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (i > 5 && screen.Ready) break; }
                Assert.That(root.GetComponent<BenchmarkRunner>().ValidateVisible(), Is.True,
                    "Stable-ID remapping must also refresh the changed item's content.");
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InAppComparisonRestoresPlayerProgress()
        {
            var root = new GameObject("armory-benchmark-isolation-test");
            var screen = root.AddComponent<InventoryScreen>();
            try
            {
                for (int i = 0; i < 180; i++) { yield return null; if (i > 20 && screen.Ready) break; }
                screen.ResetDataset(48); screen.SetMode(2);
                for (int i = 0; i < 60; i++) { yield return null; if (i > 10 && screen.Ready) break; }
                screen.Model.Upgrade(1); screen.SelectItem(11);
                var original = screen.Model; int gold = original.Gold;
                var runner = root.GetComponent<BenchmarkRunner>(); runner.BeginComparison();
                for (int i = 0; i < 6000 && (i < 5 || screen.Measuring); i++) yield return null;
                Assert.That(screen.Measuring, Is.False);
                Assert.That(screen.Model, Is.SameAs(original), "Diagnostic workloads must not replace player progress.");
                Assert.That(screen.Model.Gold, Is.EqualTo(gold));
                Assert.That(screen.SelectedId, Is.EqualTo(11));
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedBenchmarkExportRestoresInputAndProgress()
        {
            var root = new GameObject("armory-export-failure-test");
            var screen = root.AddComponent<InventoryScreen>();
            string file = System.IO.Path.GetTempFileName();
            try
            {
                for (int i = 0; i < 180; i++) { yield return null; if (i > 20 && screen.Ready) break; }
                screen.ResetDataset(48); screen.SetMode(2);
                for (int i = 0; i < 60; i++) { yield return null; if (i > 10 && screen.Ready) break; }
                var original = screen.Model;
                var runner = root.GetComponent<BenchmarkRunner>();
                var output = typeof(BenchmarkRunner).GetProperty("OutputOverride");
                Assert.That(output, Is.Not.Null, "Export path must be configurable for failure recovery validation.");
                output.SetValue(runner, file, null); runner.BeginComparison();
                for (int i = 0; i < 6000 && (i < 5 || screen.Measuring); i++) yield return null;
                Assert.That(screen.Measuring, Is.False);
                Assert.That(screen.Model, Is.SameAs(original));
                Assert.That(typeof(BenchmarkRunner).GetProperty("LastFailure").GetValue(runner, null), Is.Not.Null.And.Not.Empty);
                Click(Find(root, "奖励中心")); yield return null;
                Assert.That(Find(root, "领取奖励"), Is.Not.Null, "Input must work after export fails.");
            }
            finally { Object.Destroy(root); System.IO.File.Delete(file); }
            yield return null;
        }

        private static Selectable Find(GameObject root, string name)
        {
            foreach (var b in root.GetComponentsInChildren<Selectable>()) if (b.name == name) return b;
            Assert.Fail("Missing business action: " + name); return null;
        }
        private static void Click(Selectable button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(target, Is.EqualTo(button.gameObject), "Business action must be reachable through the real UI.");
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        }
    }
}
