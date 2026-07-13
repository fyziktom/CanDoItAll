# Proof Manifest - SB04

Status: `Completed`

## Owned Requirements

- R07, R08, R09, R11, R12, R13 contract model.

## Semantic Invariant Contract

- `bundle://proof/SB04/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing transcript: `bundle://proof/SB09/transcripts/final-validation.md`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.md`
- Changed-file hashes: `bundle://proof/SB09/changed-file-hashes.md`
- Representative SHA-256: `sha256:2d0d0d0f36431db5843e4d1597766d89989fee03210f91e72e842982d21e5b3b`

## Source Assertions

- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessSubprocessContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `SubprocessContract` | template loader source/test | runtime/application contract resolver tests | template load to assignment lifecycle | prose-only repaired handoff fails validation |
