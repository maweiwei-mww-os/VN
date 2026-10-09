using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI.Reference
{
    public sealed class EquipmentFragmentPresentation : MonoBehaviour
    {
        public int Kind;
        public Text[] Fields;
        public Image Progress;
        public Image[] Marks;
        public void Apply(Equipment item)
        {
            if (Kind == 3)
            {
                Fields[0].text = "攻 +" + item.Power / 3;
                Fields[1].text = "防 +" + item.Power / 5;
                Fields[2].text = "典藏";
            }
            else if (Kind == 4)
            {
                Fields[0].text = "精通 " + item.Mastery + "%";
                Progress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 84 * Mathf.Clamp01(item.Mastery / 100f));
            }
            else if (Kind == 5)
            {
                Fields[0].text = "套装 " + item.SetId;
                for (int i = 0; i < Marks.Length; i++) Marks[i].color = i < 2 ? UiFactory.Accent : UiFactory.Border;
            }
            else if (Kind == 6) Fields[0].text = "限时";
        }
        public static GameObject Create(int kind, Transform parent)
        {
            string[] labels = { "选中", "锁", "已装备", "典藏", "精通", "套装", "限时" };
            float[] xs = { 106, 6, 6, 100, 6, 98, 55 }, ys = { 4, 4, 26, 28, 84, 135, 4 };
            var panel = UiFactory.Panel(labels[kind], parent, xs[kind], ys[kind], kind == 4 ? 90 : 46, kind == 3 ? 51 : 20,
                kind == 3 ? UiFactory.Hex("59452B") : UiFactory.Hex("344C5C"));
            var view = panel.gameObject.AddComponent<EquipmentFragmentPresentation>(); view.Kind = kind;
            if (kind < 3 || kind == 6)
            {
                view.Fields = new[] { UiFactory.Label(panel.transform, labels[kind], 2, 0, 42, 20, 11, UiFactory.Ink, TextAnchor.MiddleCenter) };
                UiFactory.Panel("Accent edge", panel.transform, 0, 0, 2, 20, UiFactory.Accent);
            }
            else if (kind == 3)
            {
                view.Fields = new[] {
                    UiFactory.Label(panel.transform, "", 3, 1, 40, 14, 9, UiFactory.Accent),
                    UiFactory.Label(panel.transform, "", 3, 16, 40, 14, 9, UiFactory.Ink),
                    UiFactory.Label(panel.transform, "", 3, 33, 40, 14, 10, UiFactory.Accent)
                };
                UiFactory.Panel("Separator", panel.transform, 3, 31, 40, 1, UiFactory.Border);
            }
            else if (kind == 4)
            {
                view.Fields = new[] { UiFactory.Label(panel.transform, "", 3, 0, 84, 15, 10) };
                UiFactory.Panel("Progress track", panel.transform, 3, 16, 84, 3, UiFactory.Border);
                view.Progress = UiFactory.Panel("Mastery fill", panel.transform, 3, 16, 84, 3, UiFactory.Accent);
            }
            else
            {
                view.Fields = new[] { UiFactory.Label(panel.transform, "", 2, 0, 42, 13, 10, UiFactory.Accent) };
                view.Marks = new Image[4];
                for (int i = 0; i < 4; i++) view.Marks[i] = UiFactory.Panel("Set piece", panel.transform, 3 + i * 10, 15, 7, 3, UiFactory.Border);
            }
            panel.gameObject.SetActive(false); return panel.gameObject;
        }
    }
}
