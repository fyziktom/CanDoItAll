# SB03: Deterministic representative runtime matrix rerun

## Status
- Current status: Completed

## Objective
Re-run the deterministic automation matrix for Blazor, software-delivery/multi-team, business-plan PostgreSQL, runtime-host readback, and scheduler/workflow read-only jobs.

## Covered Inputs
- RN-001: Check whether processes now work like before.
- RN-004: Stabilize process functionality before further runtime extraction.

## Prerequisites
- SB02 closure gate must pass or record a precise live-provider blocker.
- Deterministic test infrastructure must be classified from actual output.

## Exact Source References
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Deliverables
- Focused integration matrix transcript.
- SuppressAutomationDispatch scan result for automation proof methods.
- PostgreSQL availability and pass/skip/fail classification.

## Dependency Impact
- SB04 may start only when deterministic runtime proof is green or a functional blocker is exact.
- SB06 final decision depends on this matrix for runtime-stable classification.

## Validation Depth
- Entry gate: confirm SB02 classification and all matrix source references.
- Closure gate: focused integration transcript, source scan transcript, and proof manifest.
- Semantic Adequacy Gate: record shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note closure in `bundle://proof/SB03/semantic-invariants.md`.

## Implementation Steps
- Run the focused matrix from the previous bundle.
- Ensure automation proof methods do not use `SuppressAutomationDispatch=true`.
- Ensure old manual contract tests remain named/classified as manual_contract or state/contract tests.
- Verify run completion, outbox completion, finalizer summaries, artifacts and execution runs.

## Scope Exceptions
- PostgreSQL-dependent tests may be classified from infrastructure availability, but a skip must not be reported as deterministic runtime pass.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Blazor automation passes.
- Software-delivery/multi-team automation passes.
- Business-plan PostgreSQL automation passes or is honestly infrastructure-classified.
- Runtime-host readback on real run/step passes.
- Scheduler/workflow jobs pass.

## Proof Required
- Focused integration transcript.
- SuppressAutomationDispatch scan for automation proof methods.
- PostgreSQL availability, skip, or fail classification.
- `bundle://proof/SB03/manifest.md` with changed-file hashes and portable artifact references.
- `bundle://proof/SB03/semantic-invariants.md` with invariant IDs cited by transcripts.

## Browser Validation Logging
- N/A: SB03 has no browser-visible behavior.

## Progression Gate
- SB04 may start only after deterministic runtime matrix is green or a functional blocker is recorded.

## Suggested Agent Prompt
- Run focused deterministic integration proof and scan for suppressed automation dispatch. Keep manual contract tests separate from automation proof.
