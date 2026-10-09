using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI
{
    public sealed class FixedVirtualizedScrollList : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;
        [SerializeField] private PooledUiView _itemPrefab;
        [SerializeField, Min(1)] private float _itemHeight = 100;
        [SerializeField, Min(0)] private float _spacing = 4;
        [SerializeField, Min(0)] private int _bufferRows = 2;
        [SerializeField, Min(0)] private int _maxRetained = 32;
        [SerializeField, Min(1)] private int _columns = 1;
        private readonly Dictionary<int, PooledUiView> _active = new Dictionary<int, PooledUiView>();
        private readonly Dictionary<int, ViewWork> _requests = new Dictionary<int, ViewWork>();
        private readonly List<int> _scratch = new List<int>();
        private readonly HashSet<long> _refreshIds = new HashSet<long>();
        private readonly HashSet<long> _batchIds = new HashSet<long>();
        private readonly Dictionary<long, int> _visibleIndices = new Dictionary<long, int>();
        private readonly Dictionary<int, PooledUiView> _remapped = new Dictionary<int, PooledUiView>();
        private UiViewPool _pool;
        private Transform _poolRoot;
        private IVirtualizedItemSource _source;
        private UiRuntime _runtime;
        private bool _dirty;
        private bool _refreshContent;
        private bool _refreshStructure;
        private int _epoch;
        private int _first, _last;
        public bool ScheduleWork { get; set; } = true;
        public bool Virtualize { get; set; } = true;
        public int ActiveCount => _active.Count;
        public int PooledCount => _pool?.FreeCount ?? 0;
        public int PendingCount => _requests.Count;
        public IEnumerable<PooledUiView> ActiveViews => _active.Values;
        public IEnumerable<KeyValuePair<int, PooledUiView>> ActiveBindings => _active;
        public int FirstRequestedIndex => _first;
        public int LastRequestedIndex => _last;
        public event Action<PooledUiView> ViewCreated;

        public void Configure(ScrollRect scroll, RectTransform viewport, RectTransform content,
            PooledUiView prefab, UiRuntime runtime, int columns = 1, float itemHeight = 100, float spacing = 4)
        {
            RecycleAll();
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            _pool?.Clear(); _pool = null;
            _scrollRect = scroll; _viewport = viewport; _content = content; _itemPrefab = prefab;
            _runtime = runtime; _columns = Mathf.Max(1, columns); _itemHeight = Mathf.Max(1, itemHeight);
            _spacing = Mathf.Max(0, spacing);
            InitializePool();
            if (isActiveAndEnabled && _scrollRect != null) _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            _dirty = true;
        }
        public void SetSource(IVirtualizedItemSource source)
        {
            RecycleAll(); _source = source; ResizeContent(); _dirty = true; _refreshContent = true;
        }
        public void RefreshVisible() { _dirty = true; _refreshContent = true; ResizeContent(); }
        public void RefreshItem(long id)
        {
            foreach (var view in _active.Values)
            {
                if (view == null || view.DataId != id) continue;
                _refreshIds.Add(id);
                _dirty = true;
                return;
            }
        }
        public void RefreshItems(IEnumerable<long> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            _batchIds.Clear();
            foreach (long id in ids) _batchIds.Add(id);
            foreach (var view in _active.Values)
            {
                if (view == null || !_batchIds.Contains(view.DataId)) continue;
                _refreshIds.Add(view.DataId);
                _dirty = true;
            }
            _batchIds.Clear();
        }
        public void RefreshStructure()
        {
            float normalized = 0;
            if (_content != null && _viewport != null)
            {
                float max = Mathf.Max(0, _content.rect.height - _viewport.rect.height);
                if (max > 0) normalized = Mathf.Clamp01(_content.anchoredPosition.y / max);
            }
            CancelRequests();
            ResizeContent();
            if (_content != null && _viewport != null)
            {
                float max = Mathf.Max(0, _content.rect.height - _viewport.rect.height);
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, normalized * max);
            }
            _refreshStructure = true;
            _dirty = true;
        }
        public void ScrollTo(float normalized)
        {
            if (_source == null || _content == null) return;
            float max = Mathf.Max(0, _content.rect.height - _viewport.rect.height);
            _content.anchoredPosition = new Vector2(0, Mathf.Clamp01(normalized) * max);
            _dirty = true;
        }
        public void TrimPool() { _pool?.Clear(); }
        private void Awake() { InitializePool(); }
        private void InitializePool()
        {
            if (_itemPrefab == null || _pool != null) return;
            if (_poolRoot == null)
            {
                var root = new GameObject(name + "_PoolRoot"); root.SetActive(false);
                _poolRoot = root.transform; _poolRoot.SetParent(transform, false);
            }
            _pool = new UiViewPool(_itemPrefab, _poolRoot, _maxRetained);
        }
        private void OnEnable()
        {
            if (_scrollRect != null) _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            Canvas.willRenderCanvases += MarkLayoutReady;
            _dirty = true;
        }
        private void OnDisable()
        {
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            Canvas.willRenderCanvases -= MarkLayoutReady;
            RecycleAll();
        }
        private void OnDestroy() { _pool?.Clear(); }
        private void OnScrollChanged(Vector2 value) { _dirty = true; }
        private void OnRectTransformDimensionsChange() { _dirty = true; }
        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            UpdateVisibleRange();
        }
        private void MarkLayoutReady()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout()) return;
            foreach (var view in _active.Values) if (view != null) view.MarkLayoutReady();
        }
        private void ResizeContent()
        {
            if (_content == null) return;
            int count = _source?.Count ?? 0;
            int rows = (count + _columns - 1) / _columns;
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0, rows * (_itemHeight + _spacing) - _spacing));
        }
        private void UpdateVisibleRange()
        {
            if (_source == null || _content == null || _viewport == null || _pool == null) return;
            if (_runtime == null) _runtime = UiRuntime.Instance;
            int count = _source.Count;
            if (count <= 0) { RecycleAll(); return; }
            float step = _itemHeight + _spacing;
            float maxY = Mathf.Max(0, _content.rect.height - _viewport.rect.height);
            float scrollY = Mathf.Clamp(_content.anchoredPosition.y, 0, maxY);
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, scrollY);
            int row = Mathf.Max(0, Mathf.FloorToInt(scrollY / step) - _bufferRows);
            int rows = Mathf.CeilToInt(_viewport.rect.height / step) + _bufferRows * 2 + 1;
            _first = Virtualize ? row * _columns : 0;
            _last = Virtualize ? Mathf.Min(count - 1, _first + rows * _columns - 1) : count - 1;
            if (_refreshStructure)
            {
                _refreshStructure = false;
                RemapStableViews();
            }
            _scratch.Clear();
            foreach (var pair in _active) if (pair.Key < _first || pair.Key > _last) _scratch.Add(pair.Key);
            foreach (int index in _scratch) RecycleIndex(index);
            _scratch.Clear();
            foreach (var pair in _requests) if (pair.Key < _first || pair.Key > _last) _scratch.Add(pair.Key);
            foreach (int index in _scratch) { _runtime?.Scheduler?.Cancel(_requests[index]); _requests.Remove(index); }
            for (int index = _first; index <= _last; index++)
            {
                if (_active.TryGetValue(index, out var view))
                {
                    Position(view.transform as RectTransform, index);
                    if (_refreshContent || _refreshIds.Contains(view.DataId) || view.DataId != _source.GetStableId(index)) Request(index);
                }
                else Request(index);
            }
            _refreshContent = false;
            _refreshIds.Clear();
        }
        private void RemapStableViews()
        {
            // Surviving stable IDs move indices without changing their pool lease or binding.
            _visibleIndices.Clear();
            _remapped.Clear();
            _scratch.Clear();
            for (int index = _first; index <= _last; index++) _visibleIndices.Add(_source.GetStableId(index), index);
            foreach (var pair in _active)
            {
                if (pair.Value != null && _visibleIndices.TryGetValue(pair.Value.DataId, out int index) && !_remapped.ContainsKey(index))
                    _remapped.Add(index, pair.Value);
                else _scratch.Add(pair.Key);
            }
            List<Exception> errors = null;
            foreach (int index in _scratch)
            {
                try { RecycleIndex(index); }
                catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            }
            _active.Clear();
            foreach (var pair in _remapped) _active.Add(pair.Key, pair.Value);
            _remapped.Clear();
            _visibleIndices.Clear();
            if (errors != null) throw new AggregateException("Structure refresh cleanup failed after releasing removed views.", errors);
        }
        private void Request(int index)
        {
            if (_requests.TryGetValue(index, out var existing))
            {
                if (_runtime != null && _runtime.Scheduler.Contains(existing) && existing.IsValid) return;
                _runtime?.Scheduler?.Cancel(existing);
                _requests.Remove(index);
            }
            if (!ScheduleWork) { EnsureView(index); return; }
            if (_runtime == null) throw new InvalidOperationException("Scheduled collections require a UiRuntime.");
            var work = new ViewWork(this, index, _epoch);
            _requests.Add(index, work);
            if (!_runtime.Enqueue(work)) _requests.Remove(index);
        }
        private void EnsureView(int index)
        {
            long id = _source.GetStableId(index);
            if (_active.TryGetValue(index, out var view))
            {
                if (view.DataId != id) { _source.Unbind(view, index); view.BeginBind(id); }
                _source.Bind(view, index);
                return;
            }
            view = _pool.Rent(_content, id);
            Position(view.transform as RectTransform, index);
            _active.Add(index, view);
            if (view is IncrementalUiView incremental) incremental.ConfigureRuntime(_runtime);
            _source.Bind(view, index);
            ViewCreated?.Invoke(view);
        }
        private void Position(RectTransform rect, int index)
        {
            if (rect == null) return;
            float width = (_viewport.rect.width - (_columns - 1) * _spacing) / _columns;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(index % _columns * (width + _spacing), -(index / _columns) * (_itemHeight + _spacing));
            rect.sizeDelta = new Vector2(width, _itemHeight);
        }
        private void RecycleIndex(int index)
        {
            if (!_active.TryGetValue(index, out var view)) return;
            _active.Remove(index);
            if (view == null) return;
            try { _source?.Unbind(view, index); } finally { _pool.Return(view); }
        }
        private void RecycleAll()
        {
            CancelRequests();
            _refreshIds.Clear();
            _refreshStructure = false;
            var indices = new int[_active.Count];
            _active.Keys.CopyTo(indices, 0);
            List<Exception> errors = null;
            foreach (int index in indices)
            {
                try { RecycleIndex(index); }
                catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            }
            if (errors != null) throw new AggregateException("Collection cleanup failed after releasing every view.", errors);
        }
        private void CancelRequests()
        {
            _epoch++;
            foreach (var request in _requests.Values) _runtime?.Scheduler?.Cancel(request);
            _requests.Clear();
        }
        private sealed class ViewWork : IKeyedUiWorkItem
        {
            private readonly FixedVirtualizedScrollList _owner;
            private readonly int _index, _epoch;
            private readonly long _id;
            public ViewWork(FixedVirtualizedScrollList owner, int index, int epoch)
            { _owner = owner; _index = index; _epoch = epoch; _id = owner._source.GetStableId(index); }
            public UiPriority Priority => UiPriority.Visible;
            public string DebugName => "Collection:" + _index;
            public UiWorkKey Key => new UiWorkKey(_owner.GetInstanceID(), _epoch, _index, 2);
            public bool IsValid => _owner != null && _owner.isActiveAndEnabled && _owner._epoch == _epoch &&
                _owner._source != null && _index < _owner._source.Count && _index >= _owner._first && _index <= _owner._last &&
                _owner._source.GetStableId(_index) == _id;
            public bool ExecuteStep()
            {
                try { if (IsValid) _owner.EnsureView(_index); return true; }
                finally
                {
                    if (_owner != null && _owner._requests.TryGetValue(_index, out var current) && ReferenceEquals(current, this))
                        _owner._requests.Remove(_index);
                }
            }
        }
    }
}
