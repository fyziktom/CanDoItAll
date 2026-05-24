# SB07-background-transfer-boundaries — Background job and database transfer boundary cleanup

## Status

Completed.

## Objective

Ensure remaining non-PostgreSQL paths are explicit test/admin tools and not normal runtime DB surfaces.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseTransferService.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
- repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- repo://tests/CanDoItAll.Tests.Integration/**


## Deliverables


1. Decide whether `InMemoryBackgroundJobQueue` is intentionally transient.
2. If transient, rename/docs/tests should make that explicit.
3. If durable execution is expected, add PostgreSQL-backed job claim or route through automation/outbox.
4. Ensure persisted Data Sources cannot save/select/transfer InMemory profiles.
5. Filter transfer source/target lists to PostgreSQL profiles only unless a test/admin override explicitly asks otherwise.
6. Add tests proving InMemory does not appear as normal Data Sources profile.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Integration and component tests.

## Implementation Steps


1. Decide whether `InMemoryBackgroundJobQueue` is intentionally transient.
2. If transient, rename/docs/tests should make that explicit.
3. If durable execution is expected, add PostgreSQL-backed job claim or route through automation/outbox.
4. Ensure persisted Data Sources cannot save/select/transfer InMemory profiles.
5. Filter transfer source/target lists to PostgreSQL profiles only unless a test/admin override explicitly asks otherwise.
6. Add tests proving InMemory does not appear as normal Data Sources profile.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] InMemory is not user-managed persistent profile.
- [ ] Transfer sources/targets are PostgreSQL-only in normal UI/API.
- [ ] Background job durability expectations are explicit.
- [ ] No hidden runtime path depends on transient in-memory queue for canonical work.


## Proof Required


- `proof/SB07-background-transfer-boundaries/manifest.md`
- tests for transfer filtering
- background queue decision note


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB07-background-transfer-boundaries` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB07-background-transfer-boundaries/`.
