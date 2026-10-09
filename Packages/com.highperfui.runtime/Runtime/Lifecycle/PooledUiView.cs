using UnityEngine;

namespace HighPerfUI
{
    public abstract class PooledUiView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _interactionGate;

        private readonly UiScope _scope = new UiScope();
        private long _leaseId;
        private int _bindingGeneration;
        private long _dataId;
        private bool _stateReady;
        private bool _layoutReady;
        private int _requiredFragments;
        public bool IsCommitted => _scope.IsAlive && _stateReady && _layoutReady && _requiredFragments == 0;
        public void ConfigureInteraction(CanvasGroup gate) { _interactionGate = gate; SetInteractionEnabled(false); }
        public void RequireFragments(int count) { _requiredFragments = Mathf.Max(0, count); EvaluateInteraction(); }
        public void FragmentReady() { _requiredFragments = Mathf.Max(0, _requiredFragments - 1); EvaluateInteraction(); }
        public void MarkLayoutReady() { _layoutReady = true; EvaluateInteraction(); }
        public void InvalidateState() { _stateReady = false; _layoutReady = false; SetInteractionEnabled(false); }
        private void EvaluateInteraction() { SetInteractionEnabled(IsCommitted); }

        public long DataId { get { return _dataId; } }
        public int BindingGeneration { get { return _bindingGeneration; } }
        public long LeaseId { get { return _leaseId; } }
        public UiScope Scope { get { return _scope; } }

        public virtual void OnRent()
        {
            _leaseId++;
            _scope.Revive();
            _stateReady = false; _layoutReady = false; _requiredFragments = 0;
            SetInteractionEnabled(false);
        }

        public virtual void BeginBind(long dataId)
        {
            _bindingGeneration++;
            _dataId = dataId;
            _stateReady = false; _layoutReady = false; _requiredFragments = 0;
            SetInteractionEnabled(false);
        }

        public UiBindingToken CaptureBinding()
        {
            return new UiBindingToken(this, _bindingGeneration, _dataId, _scope.Capture());
        }

        public UiLeaseToken CaptureLease()
        {
            return new UiLeaseToken(this, _leaseId, _scope.Capture());
        }

        public virtual void CommitBinding()
        {
            _stateReady = true;
            EvaluateInteraction();
        }

        public virtual void OnReturn()
        {
            _leaseId++;
            SetInteractionEnabled(false);
            _bindingGeneration++;
            _dataId = 0;
            _scope.Invalidate();
        }

        protected virtual void OnDestroy() { _scope.Invalidate(); }

        protected void SetInteractionEnabled(bool enabled)
        {
            if (_interactionGate == null) return;
            _interactionGate.interactable = enabled;
            _interactionGate.blocksRaycasts = enabled;
        }
    }

    public struct UiLeaseToken
    {
        private readonly PooledUiView _owner;
        private readonly long _leaseId;
        private readonly UiScopeToken _scopeToken;

        internal UiLeaseToken(PooledUiView owner, long leaseId, UiScopeToken scopeToken)
        {
            _owner = owner;
            _leaseId = leaseId;
            _scopeToken = scopeToken;
        }

        public bool IsCurrent => _owner != null && _scopeToken.IsCurrent && _owner.LeaseId == _leaseId;
    }

    public struct UiBindingToken
    {
        private readonly PooledUiView _owner;
        private readonly int _generation;
        private readonly long _dataId;
        private readonly UiScopeToken _scopeToken;

        internal UiBindingToken(PooledUiView owner, int generation, long dataId, UiScopeToken scopeToken)
        {
            _owner = owner;
            _generation = generation;
            _dataId = dataId;
            _scopeToken = scopeToken;
        }

        public bool IsCurrent
        {
            get
            {
                return _owner != null &&
                       _scopeToken.IsCurrent &&
                       _owner.BindingGeneration == _generation &&
                       _owner.DataId == _dataId;
            }
        }
    }
}
