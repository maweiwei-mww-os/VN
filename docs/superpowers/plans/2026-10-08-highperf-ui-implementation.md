# HighPerfUI Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. User explicitly selected direct execution in this session; do not stop for another planning approval.

**Goal:** Deliver an installable Unity UGUI runtime, equipment inventory reference project, measured three-mode comparison, and interview evidence.

**Architecture:** Extend the supplied v0.1 package in an isolated directory. The runtime stays business-independent; a separate reference assembly creates the inventory and owns benchmark scenarios. Core regression tests precede changes.

**Tech Stack:** Unity 2022.3.60f1c1, UGUI, C#, NUnit, Windows x64 Mono, PowerShell.

**Spec:** ../specs/2026-10-08-highperf-ui-design.md

## Global Constraints

- Preserve the source ZIP and D:/AVG/UGUIVirtualRuntimeLab.
- New work is rooted at D:/SLG/HighPerfUI, with no remote push or publication.
- ReferenceProject consumes Packages/com.highperfui.runtime via a local UPM dependency.
- No test results or performance benefits may be invented; unfinished requirements remain explicit.
- Native work uses one shared frame budget, with explicit measured compatibility exceptions.
- The user selected direct execution; implement inline, recording decisions here.

## Review Focus

1. A callback that queues new work during completion must not lose that work.
2. Disabled then reused views must not resurrect a stale fragment or asset request.
3. Same stable ID with new content and shortened collection ranges must update correctly.
4. A throwing task or cleanup must not break unrelated tasks or leak ownership.
5. An ordinary pool must be a credible baseline, not deliberately weak code.

## Task 1: Importable Package and Red Regression Tests

Files: Packages/com.highperfui.runtime/package.json; Tests/EditMode/RuntimeRegressionTests.cs; ReferenceProject/Packages/manifest.json; ReferenceProject/ProjectSettings/ProjectVersion.txt; Tools/Unity.ps1.

- [x] Import the ZIP into the new package directory without changing the input archive.
- [x] Create the reference project and local package dependency using the installed Unity version.
- [x] Add tests for snapshot isolation, throwing work cleanup, scope cleanup, same-ID refresh, and stale callbacks.
- [x] Run Unity EditMode tests and retain failing XML/log output.

## Task 2: Scheduler, Scope and State Contracts

Files: Runtime/Core/FrameBudgetScheduler.cs; Runtime/Core/IUiWorkItem.cs; Runtime/Lifecycle/UiScope.cs; Runtime/Lifecycle/PooledUiView.cs; Runtime/State/IncrementalUiView.cs; Runtime/State/UiCommandQueue.cs; Runtime/Core/UiRuntime.cs.

Interfaces: scheduler Enqueue(IUiWorkItem), RunFrame(double,int); scope Track(Action), Invalidate(), Capture(); view BeginBind(long), CaptureBinding(), CommitBinding(); command Enqueue(binding,Action).

- [x] Prove queued-next-frame and exception-isolation tests fail against v0.1.
- [x] Add structural keys, frame snapshots, cancellation, isolated errors and queue age.
- [x] Implement idempotent registered cleanup and binding-ready gates; preserve dirty updates created during callbacks.
- [x] Add command ordering/cancellation and explicit runtime injection. Revision-specific barriers remain deferred.
- [x] Run EditMode suite until green, recording failures and fixes.

## Task 3: Fragments, Assets and Pools

Files: Runtime/Fragments/LazyFragmentHost.cs; Runtime/Fragments/TemplateCatalog.cs; Runtime/Assets/AsyncSpriteSlot.cs; Runtime/Pooling/UiViewPool.cs; Runtime/Pooling/UiPoolBudget.cs; Editor/HighPerfUiValidator.cs.

Interfaces: TemplateCatalog.Validate(); LazyFragmentHost.Initialize(runtime,binding); resource providers preserve the existing cancellable request contract; UiViewPool.TryRent / Rent adapter, Return, Clear.

- [x] Test query-no-create, hidden-no-create, stale fragments, duplicate return and cleanup failures.
- [x] Implement captured generations, dependency validation and measured explicit realization.
- [x] Handle synchronous and stale callbacks without losing the newest request; release result ownership.
- [x] Add pool ownership/optional global retained-count limits and staged inactive preparation; reject wrong/double returns. No byte budget or lease-generation handle.
- [x] Run lifecycle tests and inspect resource counters.

## Task 4: Collection and Minimal Integration

Files: Runtime/Virtualization/FixedVirtualizedScrollList.cs; Runtime/Virtualization/IVirtualizedItemSource.cs; Tests/PlayMode/CollectionTests.cs; Samples~/MinimalIntegration/.

- [x] Reproduce same-ID refresh and stale range problems.
- [x] Schedule pool-miss realization, preserve latest range, cancel obsolete requests and support fixed-size grid columns.
- [x] Validate bounded views, refresh, sorting, empty data and mode close/reopen. final-expanded-ui-PlayMode.xml = 4/4.
- [x] Import package into the reference project and run collection tests.

## Task 5: Inventory Reference and Windows Player

Files: ReferenceProject/Assets/HighPerfUI.Reference/{InventoryModel,InventoryScreen,EquipmentCell,UiFactory}.cs; ReferenceProject/Assets/Editor/BuildReference.cs; ReferenceProject/Assets/Resources/.

- [x] Test deterministic data, equipment changes and stat updates before implementing model behavior. Extended filter/sort UI checks added after implementation.
- [x] Build a usable Chinese inventory with search/filter/sort, selection, stat comparison, equip and upgrade modal.
- [x] Implement A/B/C with shared art, data and interaction semantics; no runtime dependency on sample business.
- [x] Build Windows player, run automated raycast interaction smoke in PlayMode and inspect actual Player screenshots.

## Task 6: Benchmark and Evidence

Files: ReferenceProject/Assets/HighPerfUI.Reference/BenchmarkRunner.cs; Tools/Compare.ps1; Benchmarks/; Runtime/Diagnostics/.

- [x] Test percentile calculations, missing data handling and semantic equality checks.
- [x] Add independent process runs, raw-frame CSV, JSON summaries and environment metadata.
- [ ] Complete separate cold/warm, multi-page, long-soak and ablation performance distributions. Deferred, not claimed complete by this preview; combined opening/scroll/update trace and correctness close/reopen checks are complete.
- [x] Distinguish short smoke evidence from the repeated combined-workload protocol; never call it a complete per-scenario/ablation benchmark.
- [x] Write user guide, integration guide, interview narrative and remaining production limitations.
- [x] Final regression, build and review; package deliverables without Library/Temp. Extracted Windows player passed semantic/render smoke, and packaged UPM fingerprint matched the installation-tested file.

## Execution Ledger

- 2026-10-08: User explicitly requested immediate direct execution. Working in the new isolated project directory, not a worktree of the prior commercial code.
- Ruling: Use the installed 2022.3.60f1c1 editor for initial validation; support for other patches remains unverified.
- Ruling: D: has about 7.4 GB free and C: about 5.2 GB at start. Avoid duplicating old Library/build caches and use a minimal new project; do not clean unrelated files.
- Core regression: Artifacts/red-core-EditMode.xml = 6 tests, 4 expected failures. Artifacts/green-core-EditMode.xml = 6/6 passed after scheduler snapshot, continuation, exception and requeue fixes.
- Lifecycle regression: Artifacts/red-lifecycle-EditMode.xml = 12 tests, 4 failures. First green attempt = 11/12; the OnEnable probe requires ExecuteAlways in EditMode, corrected in the test fixture. Additional interaction/collection tests are now running.
- Core/lifecycle/collection/domain suites expanded to 35 EditMode tests; final-EditMode.xml = 35/35. Current PlayMode suite = 4/4, including UI raycast-driven equip/upgrade. Clean install = 4/4, with no reference business assembly.
- Independent static review found 11 issues; fixes and red/green evidence are summarized in docs/ReviewRecord.md. A bounded follow-up review found no remaining P1/P2 within that scope.
- Windows Development Player built. First hidden-window runs were invalidated for black captures. Visible pilot and repeated runs require both grid rendering and business mapping checks.
- Running 10 independent processes per A/B/C mode, 1000 items and 1200 scripted workload frames; final numbers will be recorded only after all attempts validate.
- Completed 30/30 independent runs, all passed semantics/render/attempt validation. See docs/BenchmarkReport.md for all medians and negative results. This does not close the deferred full per-scenario performance milestone above.
- 1920x1080 and 1280x800 Player rendering smoke passed and screenshots inspected. Extended UI tests (sorting, empty search, kind filtering, A/B/C recreation) = 4/4.
- Packed UPM tarball installation is verified in ValidationProject; packages-lock source is local-tarball, final-tarball-install-PlayMode.xml = 4/4.
- Delivery ZIP was extracted to a fresh Artifacts directory and its actual player launched successfully; structure, UPM fingerprint, semantic and grid-render checks passed. Evidence/delivery-verification.json records this check. No remote push.
- Preview limitations are explicit in docs/DeliveryScope.md; the design specification remains a roadmap, not a statement that every capability has shipped.
