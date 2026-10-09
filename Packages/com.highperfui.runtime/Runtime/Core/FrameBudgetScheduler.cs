using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HighPerfUI
{
    public sealed class FrameBudgetScheduler
    {
        private sealed class Entry
        {
            public IUiWorkItem Work;
            public UiWorkKey? Key;
            public int Frame;
            public bool Cancelled;
        }
        private static readonly int[] Rotation = { 0, 0, 1, 0, 1, 2, 0, 1, 2, 3 };
        private readonly Queue<Entry>[] _queues = new Queue<Entry>[4];
        private readonly Dictionary<IUiWorkItem, Entry> _pending = new Dictionary<IUiWorkItem, Entry>();
        private readonly Dictionary<UiWorkKey, Entry> _keys = new Dictionary<UiWorkKey, Entry>();
        private readonly int[] _remaining = new int[4];
        private int _cursor;
        private int _frame;
        public int PendingCount => _pending.Count;
        public bool Contains(IUiWorkItem work) => work != null && _pending.ContainsKey(work);
        public double LastFrameMs { get; private set; }
        public long FaultCount { get; private set; }
        public event Action<IUiWorkItem, Exception> Faulted;
        public int OldestWaitFrames
        {
            get { int age = 0; foreach (var e in _pending.Values) age = Math.Max(age, _frame - e.Frame); return age; }
        }
        public FrameBudgetScheduler()
        {
            for (int i = 0; i < 4; i++) _queues[i] = new Queue<Entry>(64);
        }
        public bool Enqueue(IUiWorkItem item)
        {
            if (item == null || !item.IsValid) return false;
            int priority = (int)item.Priority;
            if (priority < 0 || priority > 3) throw new ArgumentOutOfRangeException(nameof(item));
            if (_pending.ContainsKey(item)) { UiMetrics.Coalesced(); return false; }
            UiWorkKey? key = (item as IKeyedUiWorkItem)?.Key;
            if (key.HasValue && _keys.TryGetValue(key.Value, out var existing))
            {
                _pending.Remove(existing.Work);
                existing.Work = item;
                _pending.Add(item, existing);
                UiMetrics.Coalesced();
                return false;
            }
            var entry = new Entry { Work = item, Key = key, Frame = _frame };
            _pending.Add(item, entry);
            if (key.HasValue) _keys.Add(key.Value, entry);
            _queues[priority].Enqueue(entry);
            UiMetrics.Scheduled();
            return true;
        }
        public bool Cancel(IUiWorkItem work)
        {
            if (work == null || !_pending.TryGetValue(work, out var entry)) return false;
            Remove(entry);
            entry.Cancelled = true;
            return true;
        }
        private void Remove(Entry entry)
        {
            _pending.Remove(entry.Work);
            if (entry.Key.HasValue) _keys.Remove(entry.Key.Value);
        }
        public void Clear()
        {
            foreach (var queue in _queues) queue.Clear();
            _pending.Clear(); _keys.Clear();
            Array.Clear(_remaining, 0, 4);
            _cursor = 0;
        }
        public int DropOptional(UiPriority minimumPriority)
        {
            int dropped = 0;
            for (int lane = (int)minimumPriority; lane < 4; lane++)
            {
                while (_queues[lane].Count > 0)
                {
                    var entry = _queues[lane].Dequeue();
                    if (entry.Cancelled) continue;
                    Remove(entry); entry.Cancelled = true; dropped++;
                    UiMetrics.OptionalDropped();
                }
                _remaining[lane] = 0;
            }
            return dropped;
        }
        public int RunFrame(double budgetMs, int maxSteps = 2048)
        {
            _frame++;
            LastFrameMs = 0;
            if (budgetMs <= 0 || maxSteps <= 0) return 0;
            long start = Stopwatch.GetTimestamp();
            double scale = 1000.0 / Stopwatch.Frequency;
            for (int lane = 0; lane < 4; lane++) _remaining[lane] = _queues[lane].Count;
            int steps = 0;
            while (steps < maxSteps && (Stopwatch.GetTimestamp() - start) * scale < budgetMs)
            {
                Entry entry = Next();
                if (entry == null) break;
                steps++;
                if (entry.Cancelled) continue;
                // Release deduplication before callbacks so a new revision can enqueue itself.
                Remove(entry);
                var item = entry.Work;
                long stepStart = Stopwatch.GetTimestamp();
                try
                {
                    if (!item.IsValid) { UiMetrics.DroppedInvalid(); continue; }
                    bool done = item.ExecuteStep();
                    if (done) UiMetrics.Completed();
                    else if (item.IsValid && !_pending.ContainsKey(item) &&
                        (!entry.Key.HasValue || !_keys.ContainsKey(entry.Key.Value)))
                    {
                        Enqueue(item);
                        if (_pending.TryGetValue(item, out var continuation)) continuation.Frame = entry.Frame;
                    }
                }
                catch (Exception error)
                {
                    FaultCount++;
                    UiMetrics.Faulted();
                    try { Faulted?.Invoke(item, error); } catch { /* Diagnostics cannot break scheduling. */ }
                }
                finally { UiMetrics.Executed((Stopwatch.GetTimestamp() - stepStart) * scale); }
            }
            LastFrameMs = (Stopwatch.GetTimestamp() - start) * scale;
            if (LastFrameMs > budgetMs) UiMetrics.Overshoot();
            return steps;
        }
        private Entry Next()
        {
            for (int attempt = 0; attempt < Rotation.Length; attempt++)
            {
                int lane = Rotation[_cursor];
                _cursor = (_cursor + 1) % Rotation.Length;
                if (_remaining[lane] <= 0 || _queues[lane].Count == 0) continue;
                _remaining[lane]--;
                return _queues[lane].Dequeue();
            }
            return null;
        }
    }
}
