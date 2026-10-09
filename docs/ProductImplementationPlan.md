# SLG 军备整备中心 Implementation Plan

> Executor: executing-plans; scoped independent workers may implement domain and runtime tasks in parallel. User authorized direct implementation.

Goal: Upgrade the runnable Unity equipment project into a complete local armory/reward application, retaining the installable UI framework and producing verified benchmark results.

Architecture: Keep the existing UGUI inventory and runtime Package. Add transactional domain rules and versioned persistence, reusable complex reward fragments, selective collection updates and lease-safe pooling. Separate normal business navigation from diagnostic comparison.

Tech Stack: Unity 2022.3.60f1c1, UGUI, C#, NUnit, Unity Test Runner, Windows Mono player, PowerShell.

Spec: `docs/SLG_Product_Delivery_Contract_2026-10-09.md` and `docs/SLG_VirtualNode_Business_Architecture_2026-10-09.md`.

## Global Constraints

- Preserve the existing project, Package, raw benchmark records and deliverables.
- Work in the writable isolated copy; publish a new version directory on D, not silently overwrite the prior executable.
- No remote push, no game shutdown, no machine shutdown.
- Baselines share business data, assets and rendered results; no preset performance percentages.
- Full game/world/combat/network server are outside this delivery.
- Report unsupported contracts, negative results and unavailable target-device evidence.

## Review Focus

- Repeated reward/upgrade confirmation must not duplicate grants or deductions.
- Selection and equipped IDs must survive sort changes, inserts and persistence.
- UI pool return/rerent and same-binding async replacement must reject stale writes.
- Failed or corrupt save must preserve a usable recovery path and not overwrite silently.
- Screenshot and real pointer hit tests must cover business panels, not only direct method calls.

## Task 1: Domain and Save

Files: InventoryModel.cs; new InventorySaveStore.cs; Tests/Editor/ArmoryDomainTests.cs.
Interfaces: preserve Equipment.Id/Name/Kind/Rarity/Level/Power/Locked; add SetId/Mastery/ExpiresAt fields. Preserve Equip/Upgrade/Find. Add wallet, UpgradeCost, TryUpgrade(id,operationId), ClaimReward(rewardId), ToggleLock, Serialize/Restore and change event. Keep Find valid for noncontiguous stable IDs.
- [ ] Write failing domain tests for costs, insufficient resources, duplicate operation, reward insert/dedup, explicit sets, noncontiguous IDs and corrupt save recovery.
- [ ] Implement minimal domain rules and versioned atomic persistence.
- [ ] Run EditMode suite and report RED/GREEN evidence.

## Task 2: Runtime precision and reuse

Files: Package Lifecycle/PooledUiView.cs, Virtualization/FixedVirtualizedScrollList.cs, Fragments/LazyFragmentHost.cs; runtime regression tests.
Interfaces: add long LeaseId and CaptureLease(); add RefreshItem(long id), RefreshItems(IEnumerable<long> ids), RefreshStructure() preserving stable same-ID view binding; add Require(string id) and batched SetVisibleState for fragments while retaining existing APIs.
- [ ] Write failing tests for stale lease, targeted bind count, same-ID reorder and batched desired-state equivalence.
- [ ] Implement backward-compatible APIs, bounded fragment residency and synchronous explicit realization where needed.
- [ ] Run runtime suite; do not regress legacy sample or package tests.

## Task 3: Playable product UI

Files: InventoryScreen.cs split into partial product panels; EquipmentCell.cs; UiFactory.cs; BuildReference.cs; new complex fragment presentation; PlayMode tests.
- [ ] Write failing actual-pointer tests for reward grant, resource-consuming upgrade, lock filtering, normal startup navigation and saved state.
- [ ] Complete warehouse, comparison/upgrade, reward center, wallet/save state and diagnostics drawer.
- [ ] Use current gear bitmap art; make the complex fragments display real business fields shared by inventory/rewards, not synthetic unused node padding.
- [ ] Preserve exact same business visuals across comparison modes, current benchmark APIs and canonical state checks.

## Task 4: Metrics, delivery and review

Files: BenchmarkRunner.cs; Tools scripts; delivery guides.
- [ ] Add scenario-specific CPU/latency attribution and expanded semantic hash; keep raw failed attempts.
- [ ] Run all EditMode/PlayMode suites; build runnable Windows player.
- [ ] Run independent-process baseline/candidate comparisons and actual rendered screenshot checks at desktop sizes.
- [ ] Review complete changes; fix important findings with regression tests.
- [ ] Build/install Package in a clean validation project; archive source, player, package, docs and metrics; extract and run shipped copy.
- [ ] Publish into a new D version directory and launch the verified program for the user.

## Execution Decisions

- No Git mutations in the original repositories. The isolated copy is the working record; tests and progress ledger replace commit-based skill helpers here.
- Targeted domain/runtime workers are independent of the main UI implementation; Unity batch runners are serialized to avoid project locks and mixed compilation.
- A nine-point rating is an acceptance target, not a claimed result; production-mobile/online evidence remains explicitly unverified.
