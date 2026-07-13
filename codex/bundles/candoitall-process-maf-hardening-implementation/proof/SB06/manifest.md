# Proof Manifest - SB06

Status: `Completed`

## Owned Requirements

- R04, R05, R06, R13.

## Semantic Invariant Contract

- `bundle://proof/SB06/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing transcript: `bundle://proof/SB09/transcripts/final-validation.md`
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/anti-stub-audit.md`
- Changed-file hashes: `bundle://proof/SB09/changed-file-hashes.md`
- Representative SHA-256: `sha256:30468e744490ee3be912bda033c966408f845a25ef9edc69c1f3d59cecb8e348`

## Source Assertions

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260708120721_ProcessRuntimeStepArtifactDescriptors.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Content-grounded produced artifact ref | materializer/readback tests | runtime artifact ledger tests | materialization to produced slot lifecycle | raw-output-only hash rejected |
