using System;
using System.Collections;
using UnityEngine;

namespace HighPerfUI.Reference
{
    public sealed class EquipmentSpriteProvider : MonoBehaviour, IUiSpriteProvider
    {
        private sealed class Request : IUiSpriteRequest { public bool Cancelled; public void Cancel() { Cancelled = true; } }
        public Sprite[] Icons { get; private set; }
        public int LiveLeases { get; private set; }
        public int PendingRequests { get; private set; }
        private void Awake() { Icons = new Sprite[9]; for (int i = 0; i < Icons.Length; i++) Icons[i] = GearArt.Create(i); }
        public IUiSpriteRequest Load(string key, Action<UiSpriteLoadResult> completed)
        {
            int kind; if (!int.TryParse(key, out kind)) kind = 0;
            var request = new Request(); PendingRequests++; StartCoroutine(Deliver(Mathf.Abs(kind % 9), request, completed)); return request;
        }
        private IEnumerator Deliver(int kind, Request request, Action<UiSpriteLoadResult> completed)
        {
            int frames = 1 + kind % 3;
            for (int i = 0; i < frames; i++) yield return null;
            PendingRequests--; LiveLeases++; bool released = false;
            // Deliberately deliver cancelled requests too: the consumer must release stale results.
            completed(new UiSpriteLoadResult(Icons[kind], () => { if (!released) { released = true; LiveLeases--; } }));
        }
        private void OnDestroy()
        {
            if (Icons == null) return;
            foreach (var sprite in Icons) if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        }
    }
}
