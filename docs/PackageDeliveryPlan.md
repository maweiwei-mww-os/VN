# Unity UPM Package Delivery Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement these tasks with evidence before completion claims.

**Goal:** Deliver an installable Unity UGUI framework package, with optional playable business samples and reproducible tests, rather than a Windows-only demo.

**Architecture:** Keep Runtime independent of the business. Export the already implemented armory as Samples~, retain explicit adapters and lifecycle contracts, and verify the actual tarball in a separate Unity project. Existing product plans remain historical context; this plan supersedes their whole-project/Windows archive as the primary deliverable.

**Tech Stack:** Unity 2022.3.60f1c1, C#, UGUI, Unity Test Framework, UPM tarball.

**Spec:** Latest user instruction on 2026-10-09: final deliverable is a package for migrating the virtual-node idea to Unity, with senior-client interview depth. Prior authorization to execute without repeated confirmation remains applicable.

## Constraints

- Keep the original D project intact; edit the existing isolated workspace copy.
- No production certification, zero-intrusion claim, mobile/IL2CPP claim or nine-point score without evidence.
- Preserve public runtime APIs. Samples must not be Runtime dependencies.
- No assets, caches or Windows executable required to install Runtime.
- Publish the package as a new release directory; retain failed pilot evidence.

## Tasks

1. Runtime closure: write failing tests for batched immediate fragment realization, run RED, implement a single demand rebuild/dependency-aware realization path, run the full suite.
2. Package contract: fail a contract check for the missing Armory sample and version-specific architecture/migration/validation docs; export sample code mechanically from ReferenceProject; keep tests optional.
3. Verify installation: pack to .tgz, install into a separate clean project, run runtime tests without the business sample, then import all samples and run product tests. Check GUIDs, assembly boundaries and actual sample operation.
4. Publish: ship .tgz plus source package, test evidence, integration and interview documentation. Report actual verification and explicitly distinguish pending performance/target-device gates.

## Review Focus

Stale callback after rebind; missing/deleted fragment template; duplicate sample import GUIDs; tarball versus embedded-package differences; diagnostic operation overwriting business progress.

## Status

- [x] Runtime closure: source EditMode 96/96, PlayMode 9/9. Batched immediate realization and binding-callback reentrancy covered. The first OnDisable fixture did not execute in EditMode; its failed run is NOT valid RED evidence. The corrected ExecuteAlways fixture confirms the rebind occurred and passes with the production guard intact.
- [x] Package contract and sample: preview.3, mechanically exported Armory sample, bidirectional content/hash contract, required modules declared, independently reviewed.
- [x] Clean installation verification: actual final tarball, runtime-only 58/58 + 3/3; imported samples 98/98 + 9/9; installed Armory sample Windows build succeeds.
- [x] Published delivery and honest acceptance record: final tarball and companion ZIP, SHA256 verification of the ZIP-contained tarball, source/docs/test logs/raw benchmark attempts/viewports and optional Windows player included. Verification project has a relocatable tarball manifest; its extra D-drive test run produced 98 passing XML cases but stalled on editor exit and was stopped with exit -1, explicitly excluded from normal-exit acceptance. This residual issue is recorded, not called a pass.

Performance gate: 1000 records, 1200 workload frames, 3 rotated independent attempts/mode. A has one timeout and one exit without a result, so the full A/B/C gate FAILED. B/C each have three valid results with matching business digests and rendered-grid checks. Their descriptive comparison is preliminary, not an all-mode release acceptance or target-device guarantee. Two desktop viewport smoke runs passed and their main/reward images were manually inspected.
