using System;

namespace HighPerfUI
{
    [Serializable]
    public struct UiMetricsSnapshot
    {
        public long Scheduled;
        public long Coalesced;
        public long ExecutedSteps;
        public long Completed;
        public long DroppedInvalid;
        public long BudgetOvershoots;
        public long PoolRents;
        public long PoolReturns;
        public long Instantiations;
        public long DestroyedOverflow;
        public long AsyncStarted;
        public long AsyncApplied;
        public long AsyncStaleDropped;
        public long OptionalDropped;
        public long Faults;
        public long PoolHits;
        public long PoolMisses;
        public long DirtyApplies;
        public double MaxSingleStepMs;
    }

    public static class UiMetrics
    {
        private static UiMetricsSnapshot s_value;

        public static void Reset()
        {
            s_value = default(UiMetricsSnapshot);
        }

        internal static void Scheduled() { s_value.Scheduled++; }
        internal static void Coalesced() { s_value.Coalesced++; }
        internal static void Executed(double ms)
        {
            s_value.ExecutedSteps++;
            if (ms > s_value.MaxSingleStepMs) s_value.MaxSingleStepMs = ms;
        }
        internal static void Completed() { s_value.Completed++; }
        internal static void DroppedInvalid() { s_value.DroppedInvalid++; }
        internal static void Overshoot() { s_value.BudgetOvershoots++; }
        internal static void PoolRent() { s_value.PoolRents++; }
        internal static void PoolReturn() { s_value.PoolReturns++; }
        internal static void Instantiated() { s_value.Instantiations++; }
        internal static void DestroyedOverflow() { s_value.DestroyedOverflow++; }
        internal static void AsyncStarted() { s_value.AsyncStarted++; }
        internal static void AsyncApplied() { s_value.AsyncApplied++; }
        internal static void AsyncStale() { s_value.AsyncStaleDropped++; }
        internal static void OptionalDropped() { s_value.OptionalDropped++; }
        internal static void Faulted() { s_value.Faults++; }
        internal static void PoolHit() { s_value.PoolHits++; }
        internal static void PoolMiss() { s_value.PoolMisses++; }
        public static void DirtyApplied() { s_value.DirtyApplies++; }

        public static UiMetricsSnapshot Snapshot()
        {
            return s_value;
        }
    }
}
