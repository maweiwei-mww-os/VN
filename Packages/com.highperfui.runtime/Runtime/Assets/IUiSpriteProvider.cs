using System;
using UnityEngine;

namespace HighPerfUI
{
    public interface IUiSpriteRequest
    {
        void Cancel();
    }

    public struct UiSpriteLoadResult
    {
        public Sprite Sprite;
        public Action Release;

        public UiSpriteLoadResult(Sprite sprite, Action release)
        {
            Sprite = sprite;
            Release = release;
        }
    }

    /// <summary>
    /// Provider callback must execute on Unity's main thread.
    /// </summary>
    public interface IUiSpriteProvider
    {
        IUiSpriteRequest Load(string key, Action<UiSpriteLoadResult> completed);
    }
}
