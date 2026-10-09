using HighPerfUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HighPerfUI.Samples
{
    public sealed class MinimalIntegrationDemo : MonoBehaviour, IVirtualizedItemSource
    {
        private UiRuntime _runtime;
        public int Count => 2000;
        private void Start()
        {
            _runtime = UiRuntime.Instance;
            if (_runtime == null) _runtime = gameObject.AddComponent<UiRuntime>();
            if (FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform);
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); canvas.transform.SetParent(transform);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("List", typeof(RectTransform), typeof(ScrollRect)); root.transform.SetParent(canvas.transform, false);
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(480, 500);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>(); viewport.SetParent(rect, false);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.sizeDelta = Vector2.zero;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1); content.sizeDelta = new Vector2(480, 500);
            var prefab = new GameObject("Item template", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)); prefab.SetActive(false); prefab.transform.SetParent(transform);
            prefab.GetComponent<Image>().color = new Color(.13f, .2f, .17f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); label.transform.SetParent(prefab.transform, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.sizeDelta = Vector2.zero;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 22; label.alignment = TextAnchor.MiddleCenter;
            var view = prefab.AddComponent<MinimalItemView>(); view.Label = label; view.ConfigureInteraction(prefab.GetComponent<CanvasGroup>());
            var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            var collection = root.AddComponent<FixedVirtualizedScrollList>(); collection.Configure(scroll, viewport, content, view, _runtime, 1, 58, 4); collection.SetSource(this);
        }
        public long GetStableId(int index) => index + 1;
        public void Bind(PooledUiView view, int index) { ((MinimalItemView)view).SetValue(index + 1, _runtime); }
        public void Unbind(PooledUiView view, int index) { }
    }
}
