using System;
using System.Collections;
using UnityEngine;

namespace HighPerfUI
{
    public sealed class ResourcesSpriteProvider : MonoBehaviour, IUiSpriteProvider
    {
        private sealed class Request : IUiSpriteRequest
        {
            public bool Cancelled;
            public void Cancel() { Cancelled = true; }
        }

        public IUiSpriteRequest Load(string key, Action<UiSpriteLoadResult> completed)
        {
            Request request = new Request();
            StartCoroutine(LoadRoutine(key, request, completed));
            return request;
        }

        private IEnumerator LoadRoutine(string key, Request request, Action<UiSpriteLoadResult> completed)
        {
            ResourceRequest load = Resources.LoadAsync<Sprite>(key);
            yield return load;

            Sprite sprite = load.asset as Sprite;
            if (request.Cancelled)
            {
                UiMetrics.AsyncStale();
                yield break;
            }

            if (completed != null)
                completed(new UiSpriteLoadResult(sprite, null));
        }
    }
}
