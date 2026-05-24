# SB02-legacy-profile-quarantine-hardening — Final legacy DB profile cleanup and quarantine hardening

## Status

Completed.

## Objective

Finish legacy DB cleanup without hiding retired-provider strings and with stronger startup/catalog tests.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileStartupConnectionResolver.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs
- repo://tests/CanDoItAll.Tests.Unit/**
- repo://tests/CanDoItAll.Tests.Integration/**


## Deliverables


1. Remove `DatabaseProfileResolutionSource.LegacyDiscovery` if no longer used.
2. Remove or justify `DatabaseProfileStorageMode.ManagedPerProfile`.
3. Replace hidden string concatenation in `LegacyDatabaseProfileCatalogQuarantine` with explicit retired-provider constants.
4. Add an audit allowlist file or audit script exception that permits retired-provider strings only in quarantine tests/quarantine implementation.
5. Add tests for:
   - legacy provider as string,
   - legacy provider as numeric value,
   - retired source values,
   - active profile reset,
   - all-legacy catalog leading to default PostgreSQL startup.
6. Ensure typed deserialization never crashes on old catalog before quarantine runs.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Unit tests plus startup/integration tests with legacy catalog fixtures.

## Implementation Steps


1. Remove `DatabaseProfileResolutionSource.LegacyDiscovery` if no longer used.
2. Remove or justify `DatabaseProfileStorageMode.ManagedPerProfile`.
3. Replace hidden string concatenation in `LegacyDatabaseProfileCatalogQuarantine` with explicit retired-provider constants.
4. Add an audit allowlist file or audit script exception that permits retired-provider strings only in quarantine tests/quarantine implementation.
5. Add tests for:
   - legacy provider as string,
   - legacy provider as numeric value,
   - retired source values,
   - active profile reset,
   - all-legacy catalog leading to default PostgreSQL startup.
6. Ensure typed deserialization never crashes on old catalog before quarantine runs.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] No hidden `"Sql" + "ite"` style residue hiding remains.
- [ ] Retired-provider residue is explicitly allowlisted only in quarantine boundary/tests.
- [ ] Legacy catalog startup path cannot crash before UI loads.
- [ ] Active legacy profile is reset safely.


## Proof Required


- `proof/SB02-legacy-profile-quarantine-hardening/manifest.md`
- residue audit transcript
- unit/integration test transcript
- sample quarantined catalog fixture


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB02-legacy-profile-quarantine-hardening` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB02-legacy-profile-quarantine-hardening/`.
