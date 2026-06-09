# SB009 Proof Manifest

## Status
Completed.

## Objective
Gate C: prove dispatch claim semantics and hosted worker readiness.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 dispatch subset.
- Critical invariant contract: `bundle://proof/SB009/semantic-invariants.md`
- Downstream dependency: SB010-SB012 may evaluate route execution, finalizer transitions, and artifact projection only after dispatch claiming and worker readiness are proven.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `557877d7985f87c69f49a60fde8d8fa5cc1ea488af43972e1d20964c14d8a911` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB009/README.md` | `a2beb0617208346ab22bc0adfd175b484d7754544b1eb856bf868a16e6cf99bb` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB009/transcripts/dispatch-claim-worker-integration-tests.txt` | `5fb99a5d16fe9bddc01ec5e63b99ad56e47037bb90e37e69ed72266eddea70a0` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB009/transcripts/dispatch-worker-source-assertions.txt` | `f00f3af8a83d2528e4993df69948e764974b1557392ecd259da237ed066cd3be` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB009/red-team/stale-worker-finalization-rejection.txt` | `35ede07d314b3d70851f8fe7ff383512bcf5b7aaa7a8854d97178c92b61110d4` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `f9df2054418001cfaec93b75ef7dc35050cdbdd54a82bc161d4529ef05ee470e` |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `31a6ffb6c64025d6a839929e211cea36bd4ad8f2e3da1d6cf298a63d42d2b677` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessOutboxIntegrationTests.cs` | `a5591795fb3244677f69f2a3012bd4edb9bdf04697fbafda5c075b12023d3e22` |
| `repo://tests/CanDoItAll.Tests.Integration/RuntimeHostedWorkerPolicyIntegrationTests.cs` | `3579552ac3211c18938c0f02a10b3d9ee227303cbb1970b03df1fdd938011a7a` |

## Command Transcripts
- Integration: `bundle://proof/SB009/transcripts/dispatch-claim-worker-integration-tests.txt`
- Source assertions: `bundle://proof/SB009/transcripts/dispatch-worker-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB009/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB009/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team stale-worker rejection: `bundle://proof/SB009/red-team/stale-worker-finalization-rejection.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Pending automation outbox record | `ProcessOutboxService.EnqueueAutomationDispatchAsync` | `ProcessOutboxService.ProcessPendingAsync` and `ProcessOutboxDrainWorker` | Created pending and not dispatched inline from run start or step transition | Duplicate enqueue reuses the pending command rather than creating duplicate dispatch work |
| Claim/lease token | `TryClaimRecordAsync` and `ClaimPendingRecordsPostgreSqlAsync` | `ProcessClaimedAsync`, lease renewal, finalization | Only a claimed record can start an attempt and finalize canonical state | Parallel drain calls prove the same automation dispatch is not run twice |
| Lease renewal and stale finalization guard | `RenewClaimedLeaseAsync`, `RenewLeaseUntilDispatchCompletesAsync`, `TryFinalizeClaimedRecordAsync` | Long-running automation dispatch and retry path | Active dispatch renews its lease; finalization requires matching live lease ownership | Stale-worker test proves lease theft prevents canonical completion by the stale worker |
| Hosted outbox worker | `ProcessesModuleServiceCollectionExtensions` and `ProcessOutboxDrainWorker` | Local runtime background processing | Worker is registered in the source-watch lane and gated off in published lanes | Runtime hosted-worker policy tests prevent accidental worker registration drift |

## Closure
- Shallow-pass trap: A fake pass could assert that an outbox row exists without proving claim exclusion, lease renewal, stale-finalization protection, and hosted worker registration.
- Adversarial negative proof: `bundle://proof/SB009/red-team/stale-worker-finalization-rejection.txt`
- Semantic positive proof: `bundle://proof/SB009/transcripts/dispatch-claim-worker-integration-tests.txt` plus `bundle://proof/SB009/transcripts/dispatch-worker-source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB009/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Dispatch claim and hosted worker readiness are source-backed; route execution and artifact projection remain owned by SB010-SB012.
