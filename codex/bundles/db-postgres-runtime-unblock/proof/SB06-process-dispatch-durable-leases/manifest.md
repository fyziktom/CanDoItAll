# SB06 Proof Manifest

## Subbundle

SB06-process-dispatch-durable-leases — Completed.

Owned requirements: R8, R10.

Semantic invariant contract: `bundle://proof/SB06-process-dispatch-durable-leases/semantic-invariants.md`.

## Changed Files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs` | See `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv` | See hash inventory | Adds durable automation dispatch claim fields. |
| `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs` | See hash inventory | See hash inventory | Configures claim columns and index. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` | See hash inventory | See hash inventory | Adds lease duration and dispatcher identity. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | See hash inventory | See hash inventory | Moves long execution outside the in-memory guard after durable DB claim. |
| `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524144716_ProcessStepAutomationDispatchClaims.cs` | New | See hash inventory | Adds PostgreSQL migration for durable claim columns. |
| `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs` | See hash inventory | See hash inventory | Updates model snapshot. |

## Commands

| Command | Transcript path | Result |
|---|---|---|
| Focused PostgreSQL integration sweep | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | Passed 452 tests, including process dispatch/workflow filters. |
| EF pending model changes | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-ef-has-pending-model-changes.txt` | Passed; no pending model changes. |
| Source assertions | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | Shows durable claim fields and lease paths. |

## Semantic Positive Proof

Process step automation dispatch now claims work with durable PostgreSQL state: token, claimant, claimed time, lease expiry, and attempt count. The process-local semaphore is reduced to the short claim window and is not the canonical protection around long external work.

## Adversarial Negative Proof

Candidate loading skips unexpired durable claims, long-running work renews the durable lease, and stale-token completion is rejected by claim-token checks. Focused integration tests cover process dispatch and workflow execution after the change.

## Canonicality Proof

At-most-one active automation dispatch per process step is enforced by persisted claim state. In-memory guards remain only as a local fast-path serialization around the atomic claim/finalization section.

## Anti-Stub Audit

`bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no stub markers in changed production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessStepRun.AutomationDispatchClaimToken` lease | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Same dispatcher renewal/finalization paths | `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524144716_ProcessStepAutomationDispatchClaims.cs` and `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-ef-has-pending-model-changes.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` |

## Browser Validation Analytics

N/A. SB06 has no UI behavior.

## Remaining Risks

No SB06 implementation risk remains. A broader integration rerun should still be done after local PostgreSQL credential setup is fixed.
