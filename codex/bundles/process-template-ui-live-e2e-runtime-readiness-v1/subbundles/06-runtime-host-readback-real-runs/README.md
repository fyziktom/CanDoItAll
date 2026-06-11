# SB06: Runtime-host readback on real runs

## Status
- Status: Completed

## Objective
Attach runtime-host verification and dry-run readback to actual representative process runs created by SB03-SB05.

## Covered Inputs
- Raw request: continue toward generic runtime host without unsafe side effects.
- REQ-006: attach runtime-host verification/dry-run readback to real representative process runs and operator/run-detail surfaces.

## Prerequisites
- SB03-SB05 closure gates prove representative process runs with real `ProcessRunId` and `StepRunId` values.
- Existing manager facade and read-only verification services are available.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationReadback.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerRuntimeHostDryRunReadback.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionPipeline.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs

## Deliverables
- Add helper/test that runs verification/dry-run readback against real `ProcessRunId` and `StepRunId` from representative template runs.
- Verify capability key, audit id/hash, evidence count, denial metadata, no-mutation flags, and contract snapshot.
- Expose readback through existing manager facade; do not bypass host/facade boundaries.

## Dependency Impact
- SB07 scheduler/workflow verification jobs depend on this subbundle proving readback can attach to real run and step ids.
- SB08 release decision depends on runtime-host evidence being observable without approving execution-capable drivers.

## Validation Depth
- Run focused integration tests against representative run/step ids from real template automation.
- Scan dry-run and verification paths for mutation APIs and execution-capable side effects.
- Add browser proof only if run-detail UI already exposes the readback; otherwise record API proof and the explicit UI gap.
- Include semantic adequacy proof, manifest, positive/negative transcripts, source assertions, anti-stub audit, and optional screenshots under `proof/SB06/`.

## Implementation Steps
- Add helper or test flow that captures real run and step ids from representative automation.
- Invoke read-only verification and dry-run readback through the manager facade.
- Assert capability key, audit id/hash shape, evidence count, denial metadata, no-mutation flags, and contract snapshot.
- Capture API or UI readback proof and side-effect scans.

## Do Not Do
- Do not create a generic execution host.
- Do not mutate process state through verification/dry-run host.
- Do not persist dry-run result as if it were approval to execute effects.

## Acceptance Checklist
- Readback points to real run/step ids.
- Audit references are non-empty and hash-shaped.
- Denied dry-run effect surfaces are visible to operator/manager readback.
- No mutation flags remain false for all mutation permissions.

## Proof Required
- Focused integration transcript.
- Source scan for side-effect APIs in dry-run and verification host paths.

## Completion Proof
- Manifest: `bundle://proof/SB06/manifest.md`
- Semantic invariants: `bundle://proof/SB06/semantic-invariants.md`
- Focused integration transcript: `bundle://proof/SB06/transcripts/focused-integration.txt`
- Source assertions: `bundle://proof/SB06/transcripts/source-assertions.txt`
- Side-effect scan: `bundle://proof/SB06/transcripts/side-effect-scan.txt`
- Anti-stub audit: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- Code-first guard: `bundle://proof/SB06/transcripts/code-first-guard.txt`
- Failing-first source assertion: `bundle://proof/SB06/transcripts/failing-first-source-assertion.txt`
- Browser gap: `bundle://proof/SB06/transcripts/browser-gap.txt`

## Browser Validation Logging
- Required if run-detail UI exposes readback: capture route, viewport, actions, screenshot paths, and result.
- If UI does not expose readback, record API readback proof and the explicit UI gap in `reviews/01-execution-report.md`.

## Progression Gate
- SB07 may proceed only after runtime-host readback is attached to real process runs without mutation side effects.
- Reopen SB06 if scheduler/workflow verification jobs cannot reuse the manager/facade readback surface.

## Suggested Agent Prompt
- Implement SB06 by attaching verification and dry-run readback to real representative process run and step ids through existing manager/facade boundaries. Capture side-effect scans and optional run-detail UI proof under `proof/SB06/`.
