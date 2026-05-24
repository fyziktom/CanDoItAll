# SB08 — Final validation, benchmark, and merge gate

## Status

Prepared.

## Objective

Prove the branch is ready to merge.

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

- Run restore, build, unit tests, component tests, focused integration tests, and broad integration tests in a PostgreSQL-provisioned environment.
- Run Data Sources browser proof for runtime vs pending restart profile.
- Run concurrency stress tests for automation, connector, process outbox, and process dispatch claim stealing.
- Run residue audit for SQLite and old switching/drain terms.
- Run EF pending model changes proof.
- Produce before/after or baseline throughput report.
- Write final execution report with honest residual risks.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Run restore, build, unit tests, component tests, focused integration tests, and broad integration tests in a PostgreSQL-provisioned environment.
2. Run Data Sources browser proof for runtime vs pending restart profile.
3. Run concurrency stress tests for automation, connector, process outbox, and process dispatch claim stealing.
4. Run residue audit for SQLite and old switching/drain terms.
5. Run EF pending model changes proof.
6. Produce before/after or baseline throughput report.
7. Write final execution report with honest residual risks.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] All critical tests pass or blockers are exact and environment-specific.
- [ ] No retired SQLite provider runtime residue remains except explicit quarantine terms.
- [ ] No normal runtime path uses switch/drain context leases.
- [ ] No stale claim can commit.
- [ ] Merge recommendation is explicit.

## Proof Required

- `proof/SB08/manifest.md`
- `proof/SB08/final-execution-report.md`
- all command transcripts
- browser evidence
- benchmark report

## Browser Validation Logging

Required for Data Sources and restart/pending activation UI proof.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB08/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB08 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
