using UnityEngine;

namespace HighPerfUI
{
    public enum UiRuntimeState
    {
        Normal,
        Degraded
    }

    [DefaultExecutionOrder(-9000)]
    public sealed class UiRuntime : MonoBehaviour
    {
        private static UiRuntime s_instance;

        [Header("Frame Budget")]
        [SerializeField, Min(0.1f)] private float _frameBudgetMs = 1.5f;
        [SerializeField, Min(1)] private int _maxStepsPerFrame = 2048;

        [Header("Overload / FUSED-style degradation")]
        [SerializeField, Min(16)] private int _pendingFuseThreshold = 512;
        [SerializeField, Min(1)] private int _framesToFuse = 20;
        [SerializeField, Min(1)] private int _framesToRecover = 60;

        private int _overloadedFrames;
        private int _healthyFrames;

        public static UiRuntime Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = FindObjectOfType<UiRuntime>();
                return s_instance;
            }
        }

        public FrameBudgetScheduler Scheduler { get; private set; }
        public UiRuntimeState State { get; private set; }

        public float FrameBudgetMs
        {
            get { return _frameBudgetMs; }
            set { _frameBudgetMs = Mathf.Max(0.1f, value); }
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            Scheduler = new FrameBudgetScheduler();
            Scheduler.Faulted += (work, error) => Debug.LogError("[HighPerfUI] " + work.DebugName + ": " + error);
            State = UiRuntimeState.Normal;
        }

        public bool Enqueue(IUiWorkItem item)
        {
            if (item == null || Scheduler == null) return false;

            // During overload, do not let speculative/decorative work keep extending the queue.
            if (State == UiRuntimeState.Degraded && item.Priority >= UiPriority.Preload)
                return false;

            return Scheduler.Enqueue(item);
        }

        private void Update()
        {
            if (Scheduler == null) return;
            Scheduler.RunFrame(_frameBudgetMs, _maxStepsPerFrame);
            UpdateOverloadState();
        }

        private void UpdateOverloadState()
        {
            int pending = Scheduler.PendingCount;
            if (State == UiRuntimeState.Normal)
            {
                if (pending >= _pendingFuseThreshold) _overloadedFrames++;
                else _overloadedFrames = 0;

                if (_overloadedFrames >= _framesToFuse)
                {
                    State = UiRuntimeState.Degraded;
                    _overloadedFrames = 0;
                    _healthyFrames = 0;
                    Scheduler.DropOptional(UiPriority.Preload);
                }
            }
            else
            {
                if (pending <= _pendingFuseThreshold / 2) _healthyFrames++;
                else _healthyFrames = 0;

                if (_healthyFrames >= _framesToRecover)
                {
                    State = UiRuntimeState.Normal;
                    _healthyFrames = 0;
                }
            }
        }

        private void OnDestroy()
        {
            Scheduler?.Clear();
            if (s_instance == this) s_instance = null;
        }
    }
}
