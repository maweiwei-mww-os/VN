using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI.Reference
{
    public static class UiFactory
    {
        public static readonly Color Background = Hex("191B1F"), Surface = Hex("25282E"), Border = Hex("3E444B"), Ink = Hex("EBECEE"), Muted = Hex("A0A7AF"), Accent = Hex("E3BD72");
        private static Font _font;
        public static Font Font => _font != null ? _font : (_font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "Arial" }, 18));
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        public static void Stretch(RectTransform rect, float inset = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset); }
        public static Image Panel(string name, Transform parent, float x, float y, float w, float h, Color color)
        { var rect = Rect(name, parent, x, y, w, h); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        public static Text Label(Transform parent, string value, float x, float y, float w, float h, int size = 16, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = Rect("Text", parent, x, y, w, h); var text = rect.gameObject.AddComponent<Text>();
            text.font = Font; text.text = value; text.fontSize = size; text.color = color ?? Ink; text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
            return text;
        }
        public static Button Button(Transform parent, string value, float x, float y, float w, float h, UnityEngine.Events.UnityAction click, Color? color = null)
        {
            var image = Panel(value, parent, x, y, w, h, color ?? Border); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.pressedColor = new Color(.8f, .8f, .8f); button.colors = colors;
            Label(image.transform, value, 5, 0, w - 10, h, 15, null, TextAnchor.MiddleCenter);
            if (click != null) button.onClick.AddListener(click);
            return button;
        }
        public static InputField Input(Transform parent, string placeholder, float x, float y, float w, float h)
        {
            var image = Panel("Search", parent, x, y, w, h, Surface); image.raycastTarget = true;
            var input = image.gameObject.AddComponent<InputField>(); input.targetGraphic = image;
            input.textComponent = Label(image.transform, "", 12, 0, w - 24, h, 15);
            input.placeholder = Label(image.transform, placeholder, 12, 0, w - 24, h, 15, Muted);
            return input;
        }
        public static Toggle Checkbox(Transform parent, string name, float x, float y, float w, UnityEngine.Events.UnityAction<bool> changed)
        {
            var rect = Rect(name, parent, x, y, w, 36);
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var box = Panel("Box", rect, 3, 9, 18, 18, Border);
            var check = Panel("Check", box.transform, 4, 4, 10, 10, Accent);
            toggle.targetGraphic = box; toggle.graphic = check;
            Label(rect, name, 28, 0, w - 28, 36, 14, Muted);
            toggle.onValueChanged.AddListener(changed); return toggle;
        }
    }
}
