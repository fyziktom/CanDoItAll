# SB015 Semantic Invariants

- Invariant ID: SB015_INV_001
- Source raw note: Move toward a generic process driver runtime host while keeping execution-capable drivers blocked until explicit approval.
- Expected behavior: P05 Exact registry/selector and lane options is source-backed, test-backed, and does not grant effectful driver authority.
- Disallowed shallow implementation: Report-only completion, docs-only completion, non-empty output, fallback lane discovery, reflection registration, generic object dispatch, or implicit driver DI discovery.
- Failing-first test: N/A process/no production behavior change; bundle://proof/SB015/transcripts/sb015-source-assertions.txt documents the negative gate.
- Passing test: bundle://proof/SB051/transcripts/solution-build-debug.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostStatus.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionCapableDriverFutureGate.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs, repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: Verification-host and future-gate surfaces keep mutation flags false, keep dry-run/effectful surfaces denied unless explicitly approved, and keep Process Core dependency-clean.
- Red-team negative case: bundle://proof/SB057/transcripts/required-source-scans.txt and bundle://proof/SB015/transcripts/sb015-source-assertions.txt reject fallback discovery, direct module-to-driver calls, bundle path coupling, and secret leakage.
- Downstream dependency check: bundle://proof/SB051/transcripts/solution-build-debug.txt plus bundle://proof/SB051/transcripts/focused-process-runtime-verification-host-integration-matrix.txt close downstream build and focused integration dependency risk.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB015 runtime-host governance closure | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs | repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs | bundle://proof/SB051/transcripts/solution-build-debug.txt | bundle://proof/SB057/transcripts/required-source-scans.txt |
