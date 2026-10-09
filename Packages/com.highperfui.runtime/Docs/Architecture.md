# Architecture

## Problem-driven goals

The runtime is designed around four rules:

1. Structures that are not needed are not instantiated.
2. Properties that did not change are not re-applied.
3. Incomplete critical state is not interactive.
4. Expired work cannot mutate a reused view.

## Runtime flow

```text
Business data / user input
        |
        v
IncrementalUiView target state + dirty mask
        |
        v
FrameBudgetScheduler
  Interaction / Visible / Preload / Decorative
        |
        +----> LazyFragmentHost -> Unity Instantiate at explicit fragment boundaries
        +----> FixedVirtualizedScrollList -> UiViewPool -> reused views
        +----> AsyncSpriteSlot -> binding token guards old callbacks
        |
        v
Native UGUI components
```

## Important boundaries

- The scheduler cannot preempt one expensive `Instantiate`. Keep fragments small or use the pool.
- The frame budget is a runtime target, not a hard real-time guarantee.
- A fixed-height list is included because it is deterministic and inexpensive. Dynamic-height virtualization is deliberately out of v0.1.
- Pool size must be bounded. Pooling exchanges CPU work for retained memory; it is not free.
- `UiBindingToken` protects a reused view from late async results. It does not cancel shared backend IO by itself.
- Attribute dirty masks model state. One-shot commands/animations should use a separate ordered command channel rather than being folded into the dirty mask.

## Why the scheduler uses fair priority rotation

Pure strict priority can starve preload/decorative work forever in a continuously busy UI. The scheduler therefore gives Interaction and Visible work more turns without completely starving lower classes. If a game needs strict priority for a particular operation, make that operation a synchronous lightweight safety action (for example disabling a button), not a heavy instantiate job.

## Overload degradation

`UiRuntime` enters `Degraded` mode only after the pending queue stays above a configurable threshold for multiple frames. On entry it drops queued `Preload` and `Decorative` work and rejects new optional jobs until the queue recovers. Interaction and visible-state work continue.

This is a circuit breaker, not a promise that overload can never occur. Thresholds must be profiled on target hardware, and optional work must be safe to request again later.
