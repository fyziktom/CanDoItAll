# SB03-canonical-runtime-db-pooled-factory — Canonical runtime database mode and pooled DbContext factory

## Status

Completed.

## Objective

Move normal runtime DB work to a canonical PostgreSQL startup profile and remove per-context switch lease/profile-resolution overhead.

## Covered Inputs

- User asked to review the latest `db-remove-sqlite` pass.
- User asked to identify DB bottlenecks left from SQLite-limit protection.
- User asked to preserve canonicality while unblocking throughput.

## Prerequisites

See `plan/01-phase-plan.md`.

## Exact Source References


- repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs
- repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs
- repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileStartupConnectionResolver.cs
- repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs


## Deliverables


1. Introduce a canonical runtime DB service that resolves active PostgreSQL profile once during startup.
2. Register normal `IDbContextFactory<AppDbContext>` as a pooled canonical PostgreSQL factory where possible.
3. Keep a separate `IProfileDbContextFactory` or equivalent for admin/profile-specific operations.
4. Remove `DatabaseContextLease` from the normal hot path.
5. Preserve a generation identifier for the canonical runtime profile.
6. Add tests/diagnostics proving normal context creation does not call profile resolver per context.
7. Ensure design-time factory still works for EF migrations.


## Dependency Impact

This subbundle may invalidate downstream proof if it changes runtime DB identity, process execution semantics, or validation scope. Do not proceed to dependent subbundles until the progression gate passes.

## Validation Depth

Critical foundation: semantic adequacy proof, build, unit tests, targeted integration tests, and diagnostic evidence.

## Implementation Steps


1. Introduce a canonical runtime DB service that resolves active PostgreSQL profile once during startup.
2. Register normal `IDbContextFactory<AppDbContext>` as a pooled canonical PostgreSQL factory where possible.
3. Keep a separate `IProfileDbContextFactory` or equivalent for admin/profile-specific operations.
4. Remove `DatabaseContextLease` from the normal hot path.
5. Preserve a generation identifier for the canonical runtime profile.
6. Add tests/diagnostics proving normal context creation does not call profile resolver per context.
7. Ensure design-time factory still works for EF migrations.


## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not implement SQLite snapshots.

## Do Not Do

- Do not reintroduce SQLite runtime support.
- Do not weaken canonicality.
- Do not hide test failures behind broad "unrelated" claims.
- Do not remove locks before durable PostgreSQL claim proof exists.

## Acceptance Checklist


- [ ] Normal runtime contexts use canonical PostgreSQL options built once per process generation.
- [ ] Normal context creation does not acquire runtime switch lease.
- [ ] Profile-specific context creation still works for Data Sources admin actions.
- [ ] EF migration design-time context still works.
- [ ] No operation can straddle profiles.


## Proof Required


- `proof/SB03-canonical-runtime-db-pooled-factory/manifest.md`
- before/after context creation diagnostic transcript
- build/test transcript
- semantic invariant proof for canonical profile generation


## Browser Validation Logging

Record route, viewport, actions, assertions, screenshot paths, and result when UI is touched. Use N/A only if this subbundle does not touch UI.

## Progression Gate

All acceptance checklist items and proof files must exist before starting downstream subbundles.

## Suggested Agent Prompt

Execute `SB03-canonical-runtime-db-pooled-factory` from this bundle. Read the exact source references, implement only the scoped changes, then create proof under `proof/SB03-canonical-runtime-db-pooled-factory/`.
