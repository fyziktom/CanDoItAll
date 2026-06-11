# SB06: Runtime-host readback on real runs

## Status
Prepared.

## Objective
Attach runtime-host verification and dry-run readback to actual representative process runs created by SB03-SB05.

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

## Browser Validation Logging
If run-detail UI exposes this readback, capture large-screen screenshot; otherwise record API readback proof and explicit UI gap.

## Progression Gate
SB07 may proceed after runtime-host readback is attached to real process runs.
