# Reward List Demo

This sample demonstrates the intended integration path for a large, fixed-height UGUI list.

## Minimal hierarchy

1. Add a `UiRuntime` to the scene.
2. Create a normal `ScrollRect` with `Viewport` and `Content`.
3. Add `FixedVirtualizedScrollList` and wire the ScrollRect, Viewport, Content and item prefab.
4. Item prefab root uses `RewardItemView` and a `CanvasGroup` for the interaction gate.
5. Add `ResourcesSpriteProvider` to any scene object and assign it to the item's `AsyncSpriteSlot`.
6. Add `RewardListDemo`, assign the virtualized list and choose the number of logical items.
7. Add `UiFrameProbe` when doing before/after measurements.

The sample intentionally does not ship a generated prefab/scene because concrete art, anchors, fonts and sprite resources are project-specific.

## What to observe

- 10,000 logical items do not produce 10,000 active views.
- Rebinding a pooled item increments its binding generation.
- An old async sprite request cannot overwrite a newly rebound item.
- Repeated `SetData` calls before the scheduler commits coalesce at field-state level.
- `UiFrameProbe` prints P50/P95/P99/max frame time and runtime counters.
