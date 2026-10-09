using System;
using System.Collections.Generic;

namespace HighPerfUI
{
    public sealed class UiScope : IDisposable
    {
        private int _epoch;
        private bool _alive;
        private readonly List<Registration> _cleanup = new List<Registration>();
        public Exception LastCleanupError { get; private set; }

        public int Epoch { get { return _epoch; } }
        public bool IsAlive { get { return _alive; } }

        public void Revive()
        {
            if (_alive) Invalidate();
            _epoch++;
            _alive = true;
        }

        public void Invalidate()
        {
            if (!_alive && _cleanup.Count == 0) return;
            _epoch++;
            _alive = false;
            while (_cleanup.Count > 0)
            {
                int last = _cleanup.Count - 1;
                var cleanup = _cleanup[last];
                _cleanup.RemoveAt(last);
                try { cleanup.Dispose(); } catch (Exception error) { LastCleanupError = error; }
            }
        }

        public IDisposable Track(Action cleanup)
        {
            if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
            if (!_alive) { cleanup(); return new Registration(null, null); }
            var registration = new Registration(this, cleanup);
            _cleanup.Add(registration);
            return registration;
        }

        public void Dispose() { Invalidate(); }

        private sealed class Registration : IDisposable
        {
            private UiScope _scope;
            private Action _cleanup;
            public Registration(UiScope scope, Action cleanup) { _scope = scope; _cleanup = cleanup; }
            public void Dispose()
            {
                var scope = _scope; var cleanup = _cleanup;
                _scope = null; _cleanup = null;
                scope?._cleanup.Remove(this);
                cleanup?.Invoke();
            }
        }

        public UiScopeToken Capture()
        {
            return new UiScopeToken(this, _epoch);
        }
    }

    public struct UiScopeToken
    {
        private readonly UiScope _scope;
        private readonly int _epoch;

        internal UiScopeToken(UiScope scope, int epoch)
        {
            _scope = scope;
            _epoch = epoch;
        }

        public bool IsCurrent
        {
            get { return _scope != null && _scope.IsAlive && _scope.Epoch == _epoch; }
        }
    }
}
