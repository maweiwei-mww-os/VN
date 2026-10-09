# Execution ledger: docs/ProductImplementationPlan.md

2026-10-09: User authorized implementation of the agreed playable SLG armory/reward product and measured comparisons.

Ruling: Use an isolated source copy under the writable workspace, leaving the D project unchanged during implementation. Original repository Git access is denied; do not attempt to bypass it or mutate original history.

Preflight: Existing Equipment model API is consumed by screen/cells/benchmarks. Preserve existing public members while adding domain fields and commands.
Preflight: Existing runtime methods are consumed by cells/samples/tests. New targeted refresh, fragment and lease APIs must remain backward compatible.
Preflight: Baseline EditMode batch is running before implementation. Only one Unity batch process per project may run.

Baseline: Unity EditMode 35/35 passed, Artifacts/baseline-EditMode.xml.
Task 1: RED tests assigned to scoped domain worker; production changes wait for observed failures.
Task 2: RED tests assigned to scoped runtime worker; production changes wait for observed failures.
Task 3: New actual-pointer PlayMode tests written before UI changes, covering reward grant and opt-in diagnostics/locked filtering.
Task 4: pending.

RED evidence: product PlayMode 4 passed / 2 expected missing-feature failures; domain EditMode 36 passed / 24 expected domain failures; runtime EditMode 36 passed / 46 expected missing-domain/runtime failures. Runtime class had 22 new failing cases. Logs/XML retained in Artifacts.
Task 3 implementation: normal business header, opt-in diagnostics, shared complex equipment/reward cells, wallet/upgrade confirmation, selective model notifications, save handling and reward grant panel implemented; awaiting integration compile.
Asset: generated original commander portrait copied into Assets/Resources/ArmoryArt/Commander.png, inspected; no external runtime file dependency.
Task 4 implementation: per-phase cell binding CPU and expanded domain semantic digest added. Cell timings exclude full Item prefab cloning/layout/render, but C's synchronous optional-fragment realization is inside ApplyDirty and IS included. These are attributed cell-binding costs, not total UI CPU.

2026-10-09 final direction: the primary deliverable is an installable UPM package. See PackageDeliveryPlan.md for final tarball installation verification, current sample testing, and the incomplete A/B/C performance gate. Historical prototype logs and the original D project are not current release evidence.
