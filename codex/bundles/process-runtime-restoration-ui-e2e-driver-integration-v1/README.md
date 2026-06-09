# process-runtime-restoration-ui-e2e-driver-integration-v1

## Status

Prepared for Codex implementation.

## Purpose

This bundle shifts the work from driver-package stabilization back to the user-visible process runtime: starting processes from the app UI/project context and proving that process runs execute through the current refactored Process Core + Process Module + MAF/driver boundary.

## Why this bundle exists

The recent read-only driver work is useful, but it does not prove that users can actually start and run processes again. The current code also still contains tests that depend on transient `codex/bundles/<bundle-name>` paths. Those must be removed because bundles are implementation artifacts and are being deleted over time.

## High-Level Scope

- Remove bundle-path dependencies from code/tests.
- Prove app startup.
- Prove process UI launch on large screen.
- Prove process run creation, dispatch, MAF/direct-agent/workflow execution, artifact projection, and finalization.
- Prove representative `.NET app` and business-analysis scenarios.
- Keep Process Core generic and deterministic.
- Keep driver integrations read-only and side-effect-free.
- Keep runtime host/registry/selector/DI/manager/scheduler/workflow driver hooks blocked unless explicitly approved later.

## Bundle Shape

- 18 phases.
- 54 subbundles.
- Critical gate every third subbundle.
- XLSX checklist under `evidence/checklists`.
- Runtime/UI proof is large-screen only.

## Required Validation

- `dotnet build CanDoItAll.slnx --no-restore`
- full unit tests
- focused process runtime integration tests
- focused driver boundary tests
- app startup proof
- Playwright large-screen UI process-start proof
- `.NET app` scenario smoke
- business-analysis scenario smoke
- source scans for bundle-path coupling, Core reverse dependency, driver mutation/runtime-host drift, stubs, and UI/media drift
- prepared and completed bundle validators

## Validation Summary

Bundle preparation status: `Completed`
Bundle readiness gate: `Prepared and completed validators passed`
Execution status: `Completed`
Subbundle gate review: `SB001-SB054 passed`
Final closure gate: `Passed`
Browser validation analytics: `Large desktop process-start proof passed at 1900x1200`

## Final Proof Index

- Gate A through Gate Q manifests: `bundle://proof/SB003/manifest.md`, `bundle://proof/SB006/manifest.md`, `bundle://proof/SB009/manifest.md`, `bundle://proof/SB012/manifest.md`, `bundle://proof/SB015/manifest.md`, `bundle://proof/SB018/manifest.md`, `bundle://proof/SB021/manifest.md`, `bundle://proof/SB024/manifest.md`, `bundle://proof/SB027/manifest.md`, `bundle://proof/SB030/manifest.md`, `bundle://proof/SB033/manifest.md`, `bundle://proof/SB036/manifest.md`, `bundle://proof/SB039/manifest.md`, `bundle://proof/SB042/manifest.md`, `bundle://proof/SB045/manifest.md`, `bundle://proof/SB048/manifest.md`, and `bundle://proof/SB051/manifest.md`.
- Final Gate R manifest and semantic invariants: `bundle://proof/SB054/manifest.md` and `bundle://proof/SB054/semantic-invariants.md`.
- Release-candidate validation: `bundle://proof/SB046/transcripts/solution-build-no-restore.txt`, `bundle://proof/SB046/transcripts/full-unit-tests-no-restore.txt`, `bundle://proof/SB046/transcripts/focused-integration-scenario-matrix.txt`, and `bundle://proof/SB046/transcripts/large-desktop-process-start-playwright.txt`.
- Final fake-proof audit and validators: `bundle://proof/SB052/transcripts/final-fake-proof-audit.txt`, `bundle://proof/SB053/transcripts/prepared-validator-final.txt`, and `bundle://proof/SB053/transcripts/completed-validator-final.txt`.
- Handoff package: `bundle://proof/SB054/process-runtime-restoration-ui-e2e-driver-integration-v1-final-handoff.zip`.

## Reopen Triggers

Reopen the affected subbundle and rerun the nearest critical gate if any of these regressions appear:

- tests or source guards again depend on `codex/bundles/<specific-bundle-name>`;
- the app cannot start or the `/processes` UI cannot display templates/start a process at large-desktop size;
- `.NET app` or business-analysis scenarios cannot create and complete representative process runs;
- dispatch creates a run but does not advance persisted state;
- Process Core references driver packages, modules, infrastructure, UI, workspace/storage, EF, DI, or AgentFramework;
- runtime host, registry, selector, DI hook, manager command, scheduler/workflow hook, shell/Graph/file/storage/process mutation appears without an explicitly approved future gate.

