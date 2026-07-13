# Proof Manifest - SB05

Status: `Completed`

## Owned Requirements

- R07, R08, R13.

## Semantic Invariant Contract

- `bundle://proof/SB05/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing transcript: `bundle://proof/SB09/transcripts/final-validation.md`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.md`
- Changed-file hashes: `bundle://proof/SB09/changed-file-hashes.md`
- Representative SHA-256: `sha256:049c11f02392979c75b2368aa6dff506005256bbbeba5bc6eda6053f483a9bfb`

## Source Assertions

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Parent synthesized handoff artifact | bridge source/test | runtime finalization/produced slot test | child terminal to parent completion lifecycle | child folder-only evidence rejected |
