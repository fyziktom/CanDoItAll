# Proof Manifest - SB08

Status: `Completed`

## Owned Requirements

- R09, R11, R12, R13.

## Semantic Invariant Contract

- `bundle://proof/SB08/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing transcript: `bundle://proof/SB09/transcripts/final-validation.md`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.md`
- Changed-file hashes: `bundle://proof/SB09/changed-file-hashes.md`
- Representative SHA-256: `sha256:032acb5779783c7423a2ae3c5479b41250f4b526c1002ac6896bc772e9c28fff`

## Source Assertions

- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed process template contracts | template files and loader tests | runtime contract resolver tests | template load to process launch lifecycle | prose-only hard gate fails validation |
