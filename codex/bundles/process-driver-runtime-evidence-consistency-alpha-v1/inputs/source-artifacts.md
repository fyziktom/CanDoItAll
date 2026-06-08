# Source Artifacts To Recheck During Implementation

These are mandatory current-branch sources. Do not rely on memory, previous reports, or status-only proof.

## Latest completed bundle and proof
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/reviews/01-execution-report.md`
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/proof/shared/transcripts/passing-source-scans.txt`
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/architecture/08-next-bundle-decision.md`
- `repo://codex/bundles/process-driver-alpha-consumer-evidence-pipeline-v1/architecture/07-release-gate-matrix.md`

## Real production code to inspect before changing anything
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/CanDoItAll.Processes.Drivers.TranscriptVerification.csproj`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaRequest.cs`
- `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs`

## Tests to re-run and extend
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTranscriptVerificationReadOnlyAdapterTests.cs`

## Bundle skill and validation contract
- `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md`
