# SB01 — Validation evidence and scope cleanup

## Status

Prepared.

## Objective

Refresh proof and clean branch scope before deeper changes.

## Covered Inputs

- User requested review of latest `db-remove-sqlite` branch.
- User requested another DB bottleneck review after SQLite removal.
- User requested canonicality protection while unlocking PostgreSQL performance.

## Prerequisites

See dependency map in `plan/01-phase-plan.md`.

## Exact Source References

- `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
- `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Deliverables

- Run `git fetch origin` and prove local `development`, `origin/development`, and `db-remove-sqlite` ancestry.
- Decide whether committed `codex/bundles/*` and `.codex/bundles/project-structure-workflow-runs/*` artifacts are intentionally part of the branch.
- Remove unrelated proof artifacts unless explicitly retained as permanent repo documentation.
- Rerun the residue audit and record exact remaining allowed terms.
- Update the execution report with current branch status; do not rely on stale report text saying fetch failed.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Run `git fetch origin` and prove local `development`, `origin/development`, and `db-remove-sqlite` ancestry.
2. Decide whether committed `codex/bundles/*` and `.codex/bundles/project-structure-workflow-runs/*` artifacts are intentionally part of the branch.
3. Remove unrelated proof artifacts unless explicitly retained as permanent repo documentation.
4. Rerun the residue audit and record exact remaining allowed terms.
5. Update the execution report with current branch status; do not rely on stale report text saying fetch failed.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] Branch is proven based on current remote development.
- [ ] Unrelated artifacts are removed or explicitly justified.
- [ ] Execution report no longer contains stale validation claims.
- [ ] Residue audit output is captured.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/transcripts/git-status.txt`
- `proof/SB01/transcripts/branch-ancestry.txt`
- `proof/SB01/transcripts/residue-audit.txt`

## Browser Validation Logging

N/A unless UI-visible behavior is changed.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB01/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB01 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
