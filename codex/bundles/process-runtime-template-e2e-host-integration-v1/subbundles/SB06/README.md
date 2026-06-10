# SB06: Scheduler/workflow read-only verification job lifecycle

## Status
- Completed

## Objective
Scheduler/workflow read-only verification job lifecycle.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- SB05 closure gate passes.
- Manager/operator readback model includes runtime-host verification status.
- Scheduler/workflow origins are represented without direct driver hooks.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobModel.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchModels.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs

## Scope

Turn the job runner from a thin wrapper into a lifecycle-backed read-only job path.

Deliverables:
- job request/status/result model with source kind, correlation id, run/step id, requested lane, audit reference and terminal state;
- persisted lifecycle if appropriate, or explicit reason why persistence is deferred;
- scheduler-origin and workflow-origin tests that run through manager facade, not driver hooks;
- no process mutation or effectful execution.


## Dependency Impact
- This subbundle gates SB07 because contract/capability hardening must reflect the job lifecycle shape and source-kind readback.
- If validation fails, downstream work is not trustworthy.

## Validation Depth
- Critical. Requires focused tests and source assertions.
- Semantic adequacy proof must reject a thin wrapper that returns transient results without lifecycle/readback status.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Add or harden request, status, result, source-kind, correlation, audit, and terminal-state models.
- Persist lifecycle if appropriate, or record a code-level reason why persistence is deferred.
- Prove scheduler-origin and workflow-origin tests through the manager facade.
- Prove no process mutation or effectful execution occurs.

## Do Not Do
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, driver self-registration, or generic effectful runtime host.
- Do not mutate process state through drivers.
- Do not add domain-specific concepts into Process Core.
- Do not create large proof scaffolding or repeated boilerplate during execution.

## Acceptance Checklist
- Real source/test code changed unless this is an explicit inventory blocker.
- No effectful driver execution added.
- Process Core remains generic.
- Focused tests prove behavior.
- Source scans pass.
- Code-first ratio is not weakened.

## Proof Required
- Focused test transcript.
- Source scan transcript.
- `proof/SB06/manifest.md` with changed-file hashes, transcript paths, source assertions, and anti-stub audit.
- `proof/SB06/semantic-invariants.md` tying `REQ-006` to scheduler/workflow job lifecycle proof.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Proceed only after scheduler-origin and workflow-origin read-only lifecycle proof passes through manager-facing APIs.
- Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested Agent Prompt
Implement SB06 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
