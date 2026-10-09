using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HighPerfUI
{
    public sealed class UiViewPool
    {
        private readonly PooledUiView _prefab;
        private readonly Transform _inactiveRoot;
        private readonly int _maxRetained;
        private readonly Stack<PooledUiView> _free = new Stack<PooledUiView>();
        private readonly HashSet<PooledUiView> _leased = new HashSet<PooledUiView>();
        private readonly UiPoolBudget _budget;
        public int FreeCount => _free.Count;
        public int LeasedCount => _leased.Count;
        public UiViewPool(PooledUiView prefab, Transform inactiveRoot, int maxRetained, UiPoolBudget budget = null)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (inactiveRoot == null || inactiveRoot.gameObject.activeInHierarchy)
                throw new ArgumentException("Pool preparation root must be inactive", nameof(inactiveRoot));
            _prefab = prefab; _inactiveRoot = inactiveRoot;
            _maxRetained = Math.Max(0, maxRetained); _budget = budget;
        }
        public PooledUiView Rent(Transform parent) => Rent(parent, null);
        public PooledUiView Rent(Transform parent, long? bindingId)
        {
            PooledUiView view = null;
            while (_free.Count > 0 && view == null) { view = _free.Pop(); _budget?.Release(); }
            if (view == null)
            {
                view = Object.Instantiate(_prefab, _inactiveRoot, false);
                UiMetrics.Instantiated(); UiMetrics.PoolMiss();
            }
            else UiMetrics.PoolHit();
            view.gameObject.SetActive(false);
            try
            {
                view.OnRent();
                if (bindingId.HasValue) view.BeginBind(bindingId.Value);
                view.transform.SetParent(parent, false);
                _leased.Add(view);
                view.gameObject.SetActive(true);
                UiMetrics.PoolRent();
                return view;
            }
            catch { _leased.Remove(view); view.Scope.Invalidate(); DestroyView(view); throw; }
        }
        public void Return(PooledUiView view)
        {
            if (ReferenceEquals(view, null)) return;
            if (!_leased.Remove(view)) throw new InvalidOperationException("View is not leased by this pool, or was returned twice.");
            if (view == null) return;
            try { view.OnReturn(); }
            catch { view.Scope.Invalidate(); DestroyView(view); throw; }
            view.gameObject.SetActive(false);
            UiMetrics.PoolReturn();
            if (_free.Count < _maxRetained && (_budget == null || _budget.TryRetain()))
            {
                view.transform.SetParent(_inactiveRoot, false);
                _free.Push(view);
            }
            else { DestroyView(view); UiMetrics.DestroyedOverflow(); }
        }
        public void Clear()
        {
            while (_free.Count > 0)
            {
                var view = _free.Pop(); _budget?.Release();
                if (view != null) DestroyView(view);
            }
        }
        private static void DestroyView(PooledUiView view)
        {
            view.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(view.gameObject);
            else Object.DestroyImmediate(view.gameObject);
        }
    }
}
