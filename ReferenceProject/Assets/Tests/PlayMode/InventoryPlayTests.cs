using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HighPerfUI.Reference.Tests
{
    public sealed class InventoryPlayTests
    {
        [UnityTest] public IEnumerator ComplexInventoryOpensAndKeepsViewsBounded()
        {
            var type = typeof(InventoryModel).Assembly.GetType("HighPerfUI.Reference.InventoryScreen");
            Assert.That(type, Is.Not.Null, "A runnable inventory, not just a benchmark panel, is required.");
            var go = new GameObject("inventory-test");
            try
            {
                var screen = go.AddComponent(type);
                for (int i = 0; i < 160; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
                var collection = (FixedVirtualizedScrollList)type.GetProperty("Collection").GetValue(screen);
                Assert.That(go.GetComponentInChildren<UnityEngine.UI.CanvasScaler>().screenMatchMode,
                    Is.EqualTo(UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand), "Fixed-format inventory must fit wide and tall viewports without clipping");
                Assert.That(collection.ActiveCount, Is.GreaterThan(0));
                Assert.That(collection.ActiveCount, Is.LessThan(100));
                Assert.That((bool)type.GetProperty("Ready").GetValue(screen), Is.True);
                var model = (InventoryModel)type.GetProperty("Model").GetValue(screen);
                var provider = (EquipmentSpriteProvider)type.GetProperty("Provider").GetValue(screen);
                foreach (var view in collection.ActiveViews)
                    Assert.That(((EquipmentCell)view).Icon.sprite, Is.EqualTo(provider.Icons[model.Find(view.DataId).Kind]));
                var inventory = (InventoryScreen)screen;
                EquipmentCell first = null;
                foreach (var pair in collection.ActiveBindings) if (pair.Key == 0) first = (EquipmentCell)pair.Value;
                Assert.That(first, Is.Not.Null);
                inventory.Measuring = true;
                Assert.That(RaycastButton(first.GetComponent<Button>()), Is.Null, "Measurement must block manual pointer input");
                inventory.Measuring = false;
                Click(first.GetComponent<Button>());
                Assert.That(inventory.SelectedId, Is.EqualTo(first.DataId));
                Click(FindButton(go, "穿戴装备"));
                Assert.That(model.Equipped[model.Find(first.DataId).Kind], Is.EqualTo(first.DataId));
                int previousLevel = model.Find(first.DataId).Level;
                Click(FindButton(go, "强化"));
                Canvas.ForceUpdateCanvases(); yield return null;
                Click(FindButton(go, "确认强化"));
                Assert.That(model.Find(first.DataId).Level, Is.EqualTo(previousLevel + 1));
                type.GetMethod("SelectItem").Invoke(screen, new object[] { 8L });
                type.GetMethod("EquipSelected").Invoke(screen, null);
                Assert.That(model.Equipped[model.Find(8).Kind], Is.EqualTo(8));
                int power = model.Find(8).Power;
                type.GetMethod("UpgradeSelected").Invoke(screen, null);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(model.Find(8).Power, Is.GreaterThan(power));
                Click(FindButton(go, "按战力"));
                for (int i = 0; i < 30; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
                int best = 0; foreach (var item in model.Items) best = Mathf.Max(best, item.Power);
                Assert.That(model.Find(inventory.GetStableId(0)).Power, Is.EqualTo(best));
                var search = go.GetComponentInChildren<InputField>();
                search.text = "不存在的装备";
                for (int i = 0; i < 8; i++) yield return null;
                Assert.That(inventory.Count, Is.Zero); Assert.That(inventory.Collection.ActiveCount, Is.Zero);
                search.text = "";
                for (int i = 0; i < 30; i++) { Canvas.ForceUpdateCanvases(); yield return null; }
                foreach (var button in go.GetComponentsInChildren<Button>())
                    if (button.name == "武器" && ((RectTransform)button.transform).anchoredPosition.x > 280) { Click(button); break; }
                Assert.That(inventory.Count, Is.EqualTo(167));
                for (int i = 0; i < inventory.Count; i++) Assert.That(model.Find(inventory.GetStableId(i)).Kind, Is.Zero);
                Click(FindButton(go, "全部"));
                for (int mode = 0; mode < 3; mode++)
                {
                    inventory.SetMode(mode);
                    for (int i = 0; i < 180; i++) { Canvas.ForceUpdateCanvases(); yield return null; if (i > 10 && inventory.Ready && provider.PendingRequests == 0) break; }
                    Assert.That(inventory.Ready, Is.True);
                    if (mode == 0) Assert.That(inventory.Collection.ActiveCount, Is.EqualTo(1000));
                    else Assert.That(inventory.Collection.ActiveCount, Is.LessThan(100));
                    Assert.That(go.GetComponent<BenchmarkRunner>().ValidateVisible(), Is.True, "Mode switch must not retain old bindings or fragments");
                }
            }
            finally { UnityEngine.Object.Destroy(go); }
            yield return null;
        }
        private static Button FindButton(GameObject root, string name)
        { foreach (var button in root.GetComponentsInChildren<Button>()) if (button.name == name) return button; Assert.Fail("Missing button " + name); return null; }
        private static GameObject RaycastButton(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        }
        private static void Click(Button button)
        {
            var hit = RaycastButton(button); Assert.That(hit, Is.EqualTo(button.gameObject));
            ExecuteEvents.Execute(hit, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }
    }
}
