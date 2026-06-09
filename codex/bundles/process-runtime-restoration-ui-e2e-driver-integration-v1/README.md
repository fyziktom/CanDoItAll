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
