using System;
using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI
{
    public sealed class AsyncSpriteSlot : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private MonoBehaviour _providerComponent;
        [SerializeField] private Sprite _placeholder;

        private IUiSpriteProvider _provider;
        private IUiSpriteRequest _request;
        private Action _release;
        private int _slotVersion;
        public Exception LastCleanupError { get; private set; }
        public void Configure(Image image, IUiSpriteProvider provider, Sprite placeholder = null)
        { ResetSlot(); _image = image; _provider = provider; _placeholder = placeholder; }

        private void Awake()
        {
            _provider = _providerComponent as IUiSpriteProvider;
        }

        public void Bind(string key, UiBindingToken binding)
        {
            _slotVersion++;
            int version = _slotVersion;
            CancelCurrent();

            if (_image != null) { _image.sprite = _placeholder; _image.enabled = _placeholder != null; }
            if (_provider == null || string.IsNullOrEmpty(key)) return;

            UiMetrics.AsyncStarted();
            bool completedSynchronously = false;
            var request = _provider.Load(key, result =>
            {
                completedSynchronously = true;
                if (this == null || version != _slotVersion || !binding.IsCurrent)
                {
                    if (result.Release != null) result.Release();
                    UiMetrics.AsyncStale();
                    return;
                }

                _request = null;
                if (_image != null) { _image.sprite = result.Sprite; _image.enabled = result.Sprite != null; }
                _release = result.Release;
                UiMetrics.AsyncApplied();
            });
            if (!completedSynchronously && version == _slotVersion) _request = request;
        }

        public void ResetSlot()
        {
            _slotVersion++;
            CancelCurrent();
            if (_image != null) { _image.sprite = _placeholder; _image.enabled = _placeholder != null; }
        }

        private void OnDisable()
        {
            ResetSlot();
        }

        private void CancelCurrent()
        {
            var request = _request; var release = _release;
            _request = null; _release = null;
            if (_image != null) { _image.sprite = _placeholder; _image.enabled = _placeholder != null; }
            try { request?.Cancel(); } catch (Exception error) { LastCleanupError = error; }
            try { release?.Invoke(); } catch (Exception error) { LastCleanupError = error; }
        }
    }
}
