# Validation Plan

Do not claim a performance win until the project has target-device measurements.

## Compare four configurations

1. Full prefab instantiate / no pooling.
2. Lazy fragments only.
3. Trimmed prefab + ordinary bounded pool.
4. HighPerfUI runtime.

## Scenarios

- Cold open of a complex page.
- Warm reopen.
- First reveal of a previously lazy fragment.
- Fast scroll through a long list.
- Repeated updates of the same fields before a commit.
- Close page while sprite requests are in flight.
- Rapid A -> B -> A rebinding of a pooled item.
- Two busy pages active in the same frame.

## Record

- Frame P50 / P95 / P99 / max.
- Time to first interactive content.
- Time to complete visual content.
- Active native object count.
- Pool retained count and overflow destroys.
- Async started/applied/stale-dropped counts.
- Scheduler pending count and budget overshoots.
- Total allocated memory before/after repeated open-close loops.

`UiFrameProbe` captures frame percentiles, memory endpoints and HighPerfUI counters. Use Unity Profiler/ProfilerRecorder/Frame Debugger for deeper CPU, GC and rendering evidence.

## Correctness invariants

- Querying a fragment must not instantiate it unless explicitly requested.
- Rebinding a pooled item invalidates prior async results.
- Returning a view disables interaction before it can be rented again.
- Repeated state changes may coalesce; ordered gameplay/presentation commands may not be silently dropped.
- A view does not become interactive until the synchronous critical state commit completes.
