using UnityEngine;

namespace HighPerfUI
{
    /// <summary>
    /// Base class for target-state -> committed-state incremental updates.
    /// Derived classes keep their own strongly typed state fields and only mark changed bits.
    /// </summary>
    public abstract class IncrementalUiView : PooledUiView, IKeyedUiWorkItem
    {
        [SerializeField] private UiPriority _priority = UiPriority.Visible;
        [SerializeField] private UiRuntime _runtime;

        private ulong _dirtyMask;

        public UiPriority Priority { get { return _priority; } }
        public string DebugName { get { return name; } }
        public UiWorkKey Key => new UiWorkKey(GetInstanceID(), BindingGeneration, 0, 1);
        public void ConfigureRuntime(UiRuntime runtime) { _runtime = runtime; }
        public bool IsValid { get { return this != null && isActiveAndEnabled && Scope.IsAlive; } }

        protected void MarkDirty(ulong mask)
        {
            _dirtyMask |= mask;
            InvalidateState();
            UiRuntime runtime = ResolveRuntime();
            if (runtime != null && runtime.Scheduler != null)
                runtime.Enqueue(this);
        }

        public bool ExecuteStep()
        {
            if (_dirtyMask == 0)
            {
                CommitBinding();
                return true;
            }

            ulong snapshot = _dirtyMask;
            _dirtyMask = 0;
            try { ApplyDirty(snapshot); }
            catch
            {
                _dirtyMask |= snapshot;
                InvalidateState();
                throw;
            }

            if (_dirtyMask == 0)
            {
                OnStateCommitted(snapshot);
                if (_dirtyMask != 0) return false;
                CommitBinding();
                return true;
            }
            return false;
        }

        protected abstract void ApplyDirty(ulong dirtyMask);
        protected virtual void OnStateCommitted(ulong appliedMask) { }

        protected UiRuntime ResolveRuntime()
        {
            if (_runtime == null) _runtime = UiRuntime.Instance;
            return _runtime;
        }

        public override void OnReturn()
        {
            if (_runtime != null) _runtime.Scheduler?.Cancel(this);
            _dirtyMask = 0;
            base.OnReturn();
        }
    }
}
