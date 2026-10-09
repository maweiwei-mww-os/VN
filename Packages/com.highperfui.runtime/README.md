# High Performance UI Runtime for Unity UGUI

A source UPM package for Unity UGUI, version `0.3.0-preview.3`. The release targets Unity 2022.3.60f1c1 / Windows, not production-certified middleware. The tarball is the primary deliverable; a Windows executable is not required. See `Documentation~/Validation.md` for the actual verification scope. `Docs/` preserves historical v0.1 context, not a shipped-feature checklist.

## Included

- Shared frame-budget scheduler with four priority classes and fairness.
- Dirty-state incremental view base.
- Explicit lazy fragment creation (`SetVisible(false)` does not instantiate).
- Bounded reusable view pool.
- Fixed-height virtualized ScrollRect.
- Binding generation/scope guard for late async results.
- Async sprite slot and a simple `Resources` provider adapter.
- Interaction gate: pooled views wait for critical state, required fragments and layout readiness.
- Frame probe reporting P50/P95/P99/max, memory endpoints and runtime counters.
- Reward-list sample scripts for a 10,000 logical-item test.
- EditMode scheduler tests.
- Stable-ID structural remapping and targeted single/bulk item refresh.
- Explicit lease tokens separate from content-binding tokens.
- Optional playable SLG Armory sample: equipment, upgrades, mixed rewards, safe local saves and opt-in comparison tools.

## Installation

Add `package.json` from this directory via Unity Package Manager (Add package from disk), or install the delivered `.tgz` with Add package from tarball. The package identifier is `com.highperfui.runtime`.

Required baseline for this preview: Unity 2022.3 LTS with UGUI. Other Unity versions are unverified.

Import **SLG Armory Business** from the package's Samples tab, open its `Scenes/Inventory.unity`, then press Play. `Tools > HighPerfUI > Open Armory Sample` also opens that imported scene. Do not import two versions of the same sample assembly at once.

## Documentation

- `Documentation~/Integration.md`: lifecycle and API contracts.
- `Documentation~/Architecture.md`: invariants, mechanisms, complexity and tradeoffs.
- `Documentation~/Migration.md`: original lazy-node idea mapped to Unity boundaries.
- `Documentation~/Interview.md`: senior-client explanation and follow-up questions.
- `Documentation~/Validation.md`: installation tests, benchmark protocol and unverified gates.

## Required scene object

Create one `UiRuntime` in the scene and start with a conservative `Frame Budget Ms` such as 1-2 ms. That number is not a universal recommendation: profile the target device and choose the real value from frame-time headroom.

## Migration strategy

1. Start with one repeated complex item prefab.
2. Make optional subtrees independent prefabs and wire them to `LazyFragmentHost`.
3. Convert the item to `IncrementalUiView` and explicitly mark changed fields.
4. Put the item behind `UiViewPool` or `FixedVirtualizedScrollList`.
5. Route async icons through `AsyncSpriteSlot` and a project asset-provider adapter.
6. Run the reference project's A/B/C comparison before migrating more pages. Keep C/B results separate from C/A benefits.

## What this package does NOT claim

- It does not make Unity `Instantiate` preemptible.
- It does not replace UGUI rendering/layout internals.
- It does not implement dynamic-height list virtualization in v0.1.
- It does not prove any FPS or memory improvement without target-project measurements.
- It is a new Unity implementation derived from the observed problem pattern, not a claim that the original commercial project already used this architecture.
- It uses explicit adapters and view contracts, not transparent interception of arbitrary MonoBehaviours.
- Cooperative work steps are not hard preemption, and the framework is not React Fiber or an atomic three-phase scene transaction.

## Overload degradation

`UiRuntime` includes a small FUSED-style overload policy. Sustained queue growth drops/rejects Preload and Decorative work until the queue recovers. It never discards Interaction/Visible work automatically. Configure thresholds from target-device profiling rather than copying the defaults blindly.
