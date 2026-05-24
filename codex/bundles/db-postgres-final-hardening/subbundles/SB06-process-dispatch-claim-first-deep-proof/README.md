# SB06 — Process dispatch claim-first deep proof

## Status

Prepared.

## Objective

Verify that process dispatch no longer hydrates full process context before durable claim and that claim loss prevents mutation.

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


- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`


## Deliverables


1. Audit `LoadDispatchCandidateHeadersAsync` to ensure it is minimal.
2. Add test or instrumentation that candidate hydration happens only after claim.
3. Ensure `ProjectExecutionArtifactsAsync`, missing artifact recovery, subprocess projection, workflow completion, failure transitions, and branch transitions all require held claim.
4. Add stale-claim negative tests for every mutation path.
5. Review `StepDispatchGuards` to ensure it is short-lived and cannot become a bottleneck or memory leak.


## Dependency Impact

This subbundle affects downstream trust in throughput/canonicality proof.

## Validation Depth

Critical where indicated. Use source audit, focused tests, broad validation when possible, and anti-stub checks.

## Implementation Steps


1. Audit `LoadDispatchCandidateHeadersAsync` to ensure it is minimal.
2. Add test or instrumentation that candidate hydration happens only after claim.
3. Ensure `ProjectExecutionArtifactsAsync`, missing artifact recovery, subprocess projection, workflow completion, failure transitions, and branch transitions all require held claim.
4. Add stale-claim negative tests for every mutation path.
5. Review `StepDispatchGuards` to ensure it is short-lived and cannot become a bottleneck or memory leak.


## Scope Exceptions

None unless explicitly documented in proof.

## Do Not Do

- Do not hide failures behind focused tests only.
- Do not claim throughput improvement without either numeric benchmark or clearly stated limitation.
- Do not introduce new non-canonical DB source-of-truth.


## Acceptance Checklist


- [ ] Pre-claim path is minimal.
- [ ] All mutation paths require valid durable claim.
- [ ] Lost-claim tests pass.
- [ ] Local semaphore guard is proven short-lived and cleaned.


## Proof Required


- `proof/SB06/manifest.md`
- source audit for all mutation methods
- focused integration tests for claim loss


## Browser Validation Logging

N/A unless Data Sources UI or runtime/pending activation display changes.

## Progression Gate

This subbundle is complete only when proof artifacts are written under `proof/` and downstream subbundles can rely on its claims.

## Suggested Agent Prompt

Execute this subbundle exactly. Preserve canonical runtime DB invariants and create artifact-backed proof before moving to the next subbundle.
