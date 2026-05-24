# SB04-maintenance-restart-db-activation — Convert hot DB switching to maintenance/restart-first flow

## Status

Prepared.

## Objective

Stop treating Data Sources activation as transparent in-process hot switching in normal runtime.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs
- repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- repo://src/CanDoItAll.Web/Program.cs
- repo://tests/CanDoItAll.Tests.Components/**
- repo://tests/CanDoItAll.Tests.Playwright/**


## Deliverables


1. Define default behavior: activating a PostgreSQL profile writes control-plane active profile and requires restart.
2. Make hot switching available only behind explicit development/maintenance feature flag, if retained.
3. Update Data Sources UI copy and test IDs so users see restart/maintenance requirement.
4. Remove drain/wait UI assumptions from normal activation.
5. Ensure dev endpoints are explicit about hot switch vs pending restart.
6. Add browser/component tests for activation messaging.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Component tests, Playwright Data Sources proof, and runtime profile generation tests.

## Implementation Steps


1. Define default behavior: activating a PostgreSQL profile writes control-plane active profile and requires restart.
2. Make hot switching available only behind explicit development/maintenance feature flag, if retained.
3. Update Data Sources UI copy and test IDs so users see restart/maintenance requirement.
4. Remove drain/wait UI assumptions from normal activation.
5. Ensure dev endpoints are explicit about hot switch vs pending restart.
6. Add browser/component tests for activation messaging.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] Production/default activation no longer silently hot-switches active DB.
- [ ] UI clearly states restart/maintenance requirement.
- [ ] Development hot switch, if retained, is feature-flagged and tested.
- [ ] Canonical runtime profile cannot change mid-operation.


## Proof Required


- `proof/SB04-maintenance-restart-db-activation/manifest.md`
- component/browser screenshots
- API/dev endpoint transcript
- restart-required behavior test


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB04-maintenance-restart-db-activation` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB04-maintenance-restart-db-activation/`.
