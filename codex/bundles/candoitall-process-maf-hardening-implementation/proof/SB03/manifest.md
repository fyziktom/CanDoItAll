# Proof Manifest - SB03

Status: `Completed`

## Owned Requirements

- R02 and structured result-summary support for R15.

## Semantic Invariant Contract

- `bundle://proof/SB03/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing transcript: `bundle://proof/SB09/transcripts/final-validation.md`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.md`
- Changed-file hashes: `bundle://proof/SB09/changed-file-hashes.md`
- Representative SHA-256: `sha256:cc979880ce2eee56897cda5fcba586a3e52b7e15de07b51d3bb8552f2ed9d12c`

## Source Assertions

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Structured process result summary | AgentFramework persistence test | operator diagnostics parser test | execution run completion/failure lifecycle | raw prose-only summary fails parser test |
