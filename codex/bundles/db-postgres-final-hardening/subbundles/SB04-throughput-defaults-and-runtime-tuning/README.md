# SB04 — Throughput defaults and runtime tuning

## Status

Completed.

## Objective

Make bounded PostgreSQL parallelism actually active, configurable, and safe.

## Covered Inputs

- User requested review of what Codex fulfilled and skipped.
- User requested removal of DB bottlenecks left from SQLite-era protection.
- User requested preserving canonical database source-of-truth.

## Prerequisites

- Work from branch `db-remove-sqlite`.
- Do not reintroduce SQLite runtime provider, migrations, or UI.
- Keep code comments in English.
- Read `codex/skills/bundles/candoitall-bundle-execution/SKILL.md` before implementation.

## Exact Source References


- `repo://src/CanDoItAll.Modules.Automation/**`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Web/appsettings.json`


## Deliverables


1. Locate option classes and appsettings for automation/process/connector parallelism.
2. Ensure PostgreSQL defaults are conservative but greater than 1 where safe.
3. Add max bounds and validation attributes.
4. Add docs explaining partitioning and why canonicality is preserved.
5. Keep single-thread mode configurable for diagnostics.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Locate option classes and appsettings for automation/process/connector parallelism.
2. Ensure PostgreSQL defaults are conservative but greater than 1 where safe.
3. Add max bounds and validation attributes.
4. Add docs explaining partitioning and why canonicality is preserved.
5. Keep single-thread mode configurable for diagnostics.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Defaults are documented and non-accidental.
- [ ] PostgreSQL runtime can process batches with bounded parallelism.
- [ ] Partitioning prevents unsafe parallel writes to the same canonical aggregate.


## Proof Required


- `proof/SB04/manifest.md`
- source audit of option defaults and appsettings
- tests for configured max parallelism


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
