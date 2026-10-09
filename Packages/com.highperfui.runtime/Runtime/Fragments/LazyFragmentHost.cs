using System;
using System.Collections.Generic;
using UnityEngine;

namespace HighPerfUI
{
    public sealed class LazyFragmentHost : MonoBehaviour
    {
        private sealed class Fragment
        {
            public UiFragmentDescriptor Descriptor;
            public GameObject Instance;
            public bool Desired;
            public bool ExplicitDesired;
            public bool Critical;
            public FragmentWork Work;
            public int Index;
            public VirtualNodeState State;
        }
        [SerializeField] private UiRuntime _runtime;
        [SerializeField] private UiFragmentDescriptor[] _entries = new UiFragmentDescriptor[0];
        private PooledUiView _owner;
        private UiScope _localScope;
        private UiScopeToken _localToken;
        private int _generation = -1;
        private UiBindingToken _attachedBinding;
        private IDisposable _cleanup;
        private readonly Dictionary<string, Fragment> _map = new Dictionary<string, Fragment>();
        private Fragment[] _fragments = new Fragment[0];
        public int RealizedCount { get { int n = 0; foreach (var f in _map.Values) if (f.Instance != null) n++; return n; } }
        public string LastRealizationReason { get; private set; }

        private void Awake() { if (_entries.Length > 0 && _map.Count == 0) BuildMap(); }
        public void Configure(UiRuntime runtime, PooledUiView owner, UiFragmentDescriptor[] descriptors)
        {
            TemplateCatalog.Validate(descriptors);
            ResetBinding(); _cleanup?.Dispose();
            _cleanup = null;
            ClearInstances();
            _runtime = runtime; _owner = owner; _entries = descriptors;
            BuildMap(); AttachBinding();
        }
        private void BuildMap()
        {
            TemplateCatalog.Validate(_entries);
            _map.Clear();
            _fragments = new Fragment[_entries.Length];
            for (int i = 0; i < _entries.Length; i++)
            {
                var fragment = new Fragment { Descriptor = _entries[i], Index = i, State = VirtualNodeState.Virtual };
                _fragments[i] = fragment;
                _map.Add(_entries[i].Id, fragment);
            }
        }
        private void AttachBinding()
        {
            if (_owner != null)
            {
                if (_attachedBinding.IsCurrent && _generation == _owner.BindingGeneration && _cleanup != null) return;
                _cleanup?.Dispose(); _cleanup = null; ResetBinding();
                _generation = _owner.BindingGeneration;
                _attachedBinding = _owner.CaptureBinding();
                if (_owner.Scope.IsAlive) _cleanup = _owner.Scope.Track(ResetBinding);
            }
            else
            {
                if (_localScope == null) _localScope = new UiScope();
                if (!_localScope.IsAlive) _localScope.Revive();
                _localToken = _localScope.Capture();
            }
        }
        public void SetVisible(string id, bool visible)
        {
            AttachBinding();
            if (!_map.TryGetValue(id, out var fragment)) throw new ArgumentException("Unknown fragment " + id);
            fragment.ExplicitDesired = visible;
            Reconcile();
        }
        public void SetVisibleState(IReadOnlyList<bool> state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Count != _fragments.Length) throw new ArgumentException("Visibility state must match descriptor count.", nameof(state));
            AttachBinding();
            for (int i = 0; i < _fragments.Length; i++) _fragments[i].ExplicitDesired = state[i];
            Reconcile();
        }
        public void RequireVisibleState(IReadOnlyList<bool> state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Count != _fragments.Length) throw new ArgumentException("Visibility state must match descriptor count.", nameof(state));
            AttachBinding();
            if (_owner != null && !_owner.Scope.IsAlive) throw new InvalidOperationException("Require needs a live owner lease.");
            var binding = _owner != null ? _owner.CaptureBinding() : default;
            var scope = _localToken;
            int generation = _generation;
            for (int i = 0; i < _fragments.Length; i++) _fragments[i].ExplicitDesired = state[i];
            RebuildDemand();
            UpdateGate();
            try
            {
                foreach (var fragment in _fragments)
                {
                    if (!BindingIsCurrent(binding, scope, generation)) throw new OperationCanceledException("Fragment demand was cancelled by a binding change.");
                    CancelWork(fragment);
                    if (!fragment.Desired) Hide(fragment);
                }
                if (!BindingIsCurrent(binding, scope, generation)) throw new OperationCanceledException("Fragment demand was cancelled by a binding change.");
                foreach (var fragment in _fragments)
                    if (fragment.Desired) RequireFragment(fragment, binding, scope, generation);
            }
            finally { UpdateGate(); }
        }

        private void RebuildDemand()
        {
            foreach (var entry in _fragments) { entry.Desired = false; entry.Critical = false; }
            foreach (var entry in _fragments)
                if (entry.ExplicitDesired) Demand(entry, entry.Descriptor.RequiredForInteraction);
        }
        private void Reconcile()
        {
            RebuildDemand();
            UpdateGate();
            foreach (var entry in _fragments)
            {
                if (entry.Desired) Request(entry);
                else Hide(entry);
            }
            UpdateGate();
        }
        private void Hide(Fragment fragment)
        {
            CancelWork(fragment);
            if (fragment.Instance != null) fragment.Instance.SetActive(false);
            fragment.State = fragment.Instance != null ? VirtualNodeState.Recycled : VirtualNodeState.Virtual;
        }
        private void CancelWork(Fragment fragment)
        {
            var work = fragment.Work;
            fragment.Work = null;
            if (work != null) _runtime?.Scheduler?.Cancel(work);
        }
        private void Demand(Fragment fragment, bool critical)
        {
            critical |= fragment.Descriptor.RequiredForInteraction;
            if (fragment.Desired && (!critical || fragment.Critical)) return;
            fragment.Desired = true;
            fragment.Critical |= critical;
            var dependencies = fragment.Descriptor.Dependencies;
            if (dependencies != null) foreach (var id in dependencies) Demand(_map[id], critical);
        }
        private void Request(Fragment fragment)
        {
            if (fragment.Instance != null) { fragment.Instance.SetActive(true); fragment.State = VirtualNodeState.Real; return; }
            if (fragment.State == VirtualNodeState.Realizing || fragment.State == VirtualNodeState.Failed) return;
            if (_owner != null && !_owner.Scope.IsAlive) return;
            if (_runtime == null) _runtime = UiRuntime.Instance;
            if (_runtime == null) throw new InvalidOperationException("Lazy fragments require a UiRuntime.");
            if (fragment.Work != null && _runtime.Scheduler.Contains(fragment.Work))
            {
                if (fragment.Work.Critical == fragment.Critical) return;
                CancelWork(fragment);
            }
            fragment.Work = null;
            if (!fragment.Critical && _runtime.State == UiRuntimeState.Degraded && fragment.Descriptor.Priority >= UiPriority.Preload) return;
            fragment.Work = new FragmentWork(this, fragment, _owner != null ? _owner.CaptureBinding() : default, _localToken, _generation);
            fragment.State = VirtualNodeState.Queued;
            if (!_runtime.Enqueue(fragment.Work) && !_runtime.Scheduler.Contains(fragment.Work))
            { fragment.Work = null; fragment.State = VirtualNodeState.Virtual; }
        }
        private void LateUpdate()
        {
            if (_owner != null && !_owner.Scope.IsAlive) { ResetBinding(); return; }
            AttachBinding();
            foreach (var fragment in _fragments)
                if (fragment.Desired && fragment.Instance == null && fragment.State != VirtualNodeState.Failed) Request(fragment);
        }
        public GameObject Require(string id)
        {
            AttachBinding();
            if (!_map.TryGetValue(id, out var fragment)) throw new ArgumentException("Unknown fragment " + id);
            if (_owner != null && !_owner.Scope.IsAlive) throw new InvalidOperationException("Require needs a live owner lease.");
            var binding = _owner != null ? _owner.CaptureBinding() : default;
            var scope = _localToken;
            int generation = _generation;
            fragment.ExplicitDesired = true;
            RebuildDemand();
            try
            {
                foreach (var entry in _fragments)
                {
                    if (!BindingIsCurrent(binding, scope, generation)) throw new OperationCanceledException("Fragment demand was cancelled by a binding change.");
                    if (!entry.Desired) Hide(entry);
                }
                UpdateGate();
                RequireFragment(fragment, binding, scope, generation);
                return fragment.Instance;
            }
            finally { UpdateGate(); }
        }
        private void RequireFragment(Fragment fragment, UiBindingToken binding, UiScopeToken scope, int generation)
        {
            CancelWork(fragment);
            try
            {
                var dependencies = fragment.Descriptor.Dependencies;
                if (dependencies != null)
                    foreach (var id in dependencies) RequireFragment(_map[id], binding, scope, generation);
                if (!Realize(fragment, binding, scope, generation, "EXPLICIT_REQUIRE"))
                    throw new OperationCanceledException("Fragment realization was cancelled by a binding change.");
            }
            catch
            {
                if (BindingIsCurrent(binding, scope, generation)) fragment.State = VirtualNodeState.Failed;
                throw;
            }
        }
        private bool BindingIsCurrent(UiBindingToken binding, UiScopeToken scope, int generation)
        {
            return this != null && _generation == generation && (_owner != null ? _owner.BindingGeneration == generation && binding.IsCurrent : scope.IsCurrent);
        }
        private bool Realize(Fragment fragment, UiBindingToken binding, UiScopeToken scope, int generation, string reason)
        {
            if (!BindingIsCurrent(binding, scope, generation) || !fragment.Desired) return false;
            if (fragment.State == VirtualNodeState.Realizing) throw new InvalidOperationException("Reentrant fragment realization is not supported.");
            var instance = fragment.Instance;
            bool created = false;
            fragment.State = VirtualNodeState.Realizing;
            try
            {
                if (instance == null)
                {
                    LastRealizationReason = reason;
                    if (fragment.Descriptor.Prefab == null)
                        throw new ArgumentException("Fragment " + fragment.Descriptor.Id + " requires a live prefab.");
                    var parent = fragment.Descriptor.Parent != null ? fragment.Descriptor.Parent : transform;
                    instance = Instantiate(fragment.Descriptor.Prefab, parent, false);
                    created = true;
                    UiMetrics.Instantiated();
                }
                if (!BindingIsCurrent(binding, scope, generation) || !fragment.Desired)
                {
                    if (created) DestroyInstance(instance);
                    return false;
                }
                instance.SetActive(true);
                if (!BindingIsCurrent(binding, scope, generation) || !fragment.Desired)
                {
                    if (created) DestroyInstance(instance);
                    return false;
                }
                fragment.Instance = instance;
                fragment.State = VirtualNodeState.Real;
                return true;
            }
            catch
            {
                if (created && instance != null) DestroyInstance(instance);
                if (BindingIsCurrent(binding, scope, generation)) fragment.State = VirtualNodeState.Failed;
                throw;
            }
        }
        public void TrimHidden(int maxHidden)
        {
            maxHidden = Mathf.Max(0, maxHidden);
            int retained = 0;
            foreach (var fragment in _fragments)
            {
                if (fragment.Instance == null || fragment.Desired || fragment.Critical ||
                    fragment.Descriptor.RequiredForInteraction || fragment.Instance.activeSelf) continue;
                if (retained++ < maxHidden) continue;
                CancelWork(fragment);
                var instance = fragment.Instance;
                fragment.Instance = null;
                fragment.State = VirtualNodeState.Destroyed;
                DestroyInstance(instance);
            }
        }
        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }
        private void ClearInstances()
        {
            foreach (var fragment in _fragments)
            {
                var instance = fragment.Instance;
                fragment.Instance = null;
                DestroyInstance(instance);
            }
        }
        public GameObject GetIfCreated(string id) => _map.TryGetValue(id, out var fragment) ? fragment.Instance : null;
        public bool IsCreated(string id) => GetIfCreated(id) != null;
        public VirtualNodeState GetState(string id) => _map[id].State;
        public void ResetBinding()
        {
            foreach (var fragment in _fragments)
            {
                fragment.Desired = false;
                fragment.ExplicitDesired = false;
                fragment.Critical = false;
                CancelWork(fragment);
                if (fragment.Instance != null) fragment.Instance.SetActive(false);
                fragment.State = fragment.Instance != null ? VirtualNodeState.Recycled : VirtualNodeState.Virtual;
            }
            UpdateGate();
        }
        private void UpdateGate()
        {
            if (_owner == null) return;
            int required = 0;
            foreach (var f in _fragments) if (f.Desired && f.Critical && f.Instance == null) required++;
            _owner.RequireFragments(required);
        }
        private void OnDisable() { _localScope?.Invalidate(); ResetBinding(); }
        private void OnDestroy() { _cleanup?.Dispose(); _localScope?.Invalidate(); ResetBinding(); ClearInstances(); }

        private sealed class FragmentWork : IKeyedUiWorkItem
        {
            private readonly LazyFragmentHost _host;
            private readonly Fragment _fragment;
            private readonly UiBindingToken _binding;
            private readonly UiScopeToken _scope;
            private readonly int _generation;
            public readonly bool Critical;
            public FragmentWork(LazyFragmentHost host, Fragment fragment, UiBindingToken binding, UiScopeToken scope, int generation)
            { _host = host; _fragment = fragment; _binding = binding; _scope = scope; _generation = generation; Critical = fragment.Critical; }
            public UiWorkKey Key => new UiWorkKey(_host.GetInstanceID(), _generation, _fragment.Index, 3);
            public UiPriority Priority => Critical ? UiPriority.Visible : _fragment.Descriptor.Priority;
            public string DebugName => "Fragment:" + _fragment.Descriptor.Id;
            public bool IsValid => _host != null && _host.isActiveAndEnabled && _fragment.Desired &&
                ReferenceEquals(_fragment.Work, this) && _host.BindingIsCurrent(_binding, _scope, _generation);
            public bool ExecuteStep()
            {
                bool complete = true;
                try
                {
                    if (!IsValid) return true;
                    var dependencies = _fragment.Descriptor.Dependencies;
                    if (dependencies != null) foreach (var id in dependencies)
                    {
                        if (_host.GetState(id) == VirtualNodeState.Failed)
                        { _fragment.State = VirtualNodeState.Failed; return true; }
                        if (!_host.IsCreated(id)) { complete = false; return false; }
                    }
                    _host.Realize(_fragment, _binding, _scope, _generation, Critical ? "SCHEDULED_REQUIRED" : "SCHEDULED_OPTIONAL");
                    return true;
                }
                finally
                {
                    if (complete && ReferenceEquals(_fragment.Work, this)) _fragment.Work = null;
                    if (_host != null) _host.UpdateGate();
                }
            }
        }
    }
}
