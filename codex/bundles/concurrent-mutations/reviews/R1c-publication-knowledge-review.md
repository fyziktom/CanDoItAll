# R1c activity publication must preserve checkpoint knowledge

Independent source review of frozen inventory `8CEA93B8B42FAE403644D6F709AF8FB8DF6402D600B9B354E1C546A36AA618E0`, 2026-10-16. No build, test or database work was performed. Parent accepted the finding and held the R1c transfer pending a narrow author amendment. This record does not supersede that historical inventory or claim runtime closure.

## Blocking finding

`AgentExecutionEffectReconciliation.ReconcileAsync` acknowledges the checkpoint before publishing `AwaitingContinuation`. Its cleanup-failure branch calls `activity.SuspendForContinuation` before throwing `AgentEffectReconciliationCleanupException` (lines 108–109 in the reviewed source); its ordinary confirmed branch does the same before returning the result (line 120). The default interface method in `AgentExecutionActivityCoordinator.cs` calls `Report` and then `Suspend`. The concrete implementation calls the stream's `Append`; transition, publication or terminalization failure can throw.

Consequently an activity exception can replace a confirmed checkpoint and, in the cleanup branch, also erase the distinct lease-release uncertainty. This is an observational failure after persistence. It must not become an ordinary transient execution failure or suggest a replacement dispatch.

The encompassing paths also require review: `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs::ExecuteWithinActivityBoundaryAsync` reads `IsTerminal` and terminalizes failures; `AgentFrameworkWorkspaceService.ExecutionFacade.cs::AwaitOwnedActivityOperationAsync` repeats those operations and disposes the activity in `finally`. Their exceptions can mask a typed reconciliation failure or a confirmed result after the inner method has correctly classified it. Repairing only the inner `SuspendForContinuation` call is insufficient.

## Required narrow behavior

- Preserve the exact original continuation when its checkpoint is confirmed, including any independently known coordination-lease cleanup failure. Activity publication must not downgrade that knowledge or fabricate provider output, completion, approval, or retry permission.
- Preserve an existing incomplete continuation and its `NotAttempted`/`Unconfirmed` status when another activity error occurs. Preserve the separate no-evidence denial/unavailability/cancellation/admission failure classification; activity cleanup cannot manufacture an authorized continuation.
- Keep diagnostic exceptions available internally, with sanitized public messages. Do not relabel an activity failure as a file-lease failure merely to reuse an inaccurate error type. Avoid a broad execution-result framework or general retry change.
- Current-read refusal still withholds receipt evidence. A final current-read failure after checkpoint persistence is not proof that the checkpoint failed; subsequent same-run read remains subject to current authorization.
- The Process consumer must retain the original execution ID and refuse automatic replacement execution for the amended typed outcome, even if a nested publication/cleanup exception resembles a transient timeout.

Use the existing publication exception and best-effort activity boundaries where their semantics fit; a narrow typed knowledge carrier or facet is preferable to discarding the exact result. The author must choose a coherent shape that survives inner publication, outer `IsTerminal`, terminalization and activity disposal without changing ordinary provider-success behavior.

## Proof needed before transfer

Inject an activity failure after an actual confirmed checkpoint and verify fresh stored run/receipt evidence plus exact returned or typed continuation knowledge. Combine that failure with an actual released file lease whose acknowledgement throws, retaining both classifications and diagnostics. Cover an existing unconfirmed or denied reconciliation plus activity failure, proving no unauthorized evidence appears and the original failure is not replaced. Exercise the public wrapper path, including activity disposal/terminal observation, rather than only invoking the inner helper. Keep source-derived row counts explicit; arrangements may extend an existing Fact where cohesive.

## Other reviewed semantics

No additional blocker was found in the examined current-read identity/profile checks, pinned final revision/journal/approval CAS, incomplete-plus-cleanup exception preservation, no-evidence lease exception, or Process's explicit refusal of transient retry for the existing three typed reconciliation failures. `ExecutionRunResult` and `AgentChatRunResult` require confirmed continuation, preserve the original IDs, reject metrics/provider output/completion for that variant, and retain normal-result null checks. These are source conclusions only. Bounded lookup count does not establish acceptable owner-store allocation or write amplification; that separate measured performance gate remains open.

## Author amendment: independent source review

The amended managed source addresses the blocking paths above. This is a source review, not execution closure. `AgentExecutionEffectReconciliation` now returns the pure checkpoint result or typed failure after file-lease cleanup. The existing execution boundary publishes afterward. The Core owned facade, `CurrentProfileAgentFrameworkWorkspaceService` owned facade, and native `AgentChatExecutionOrchestrator.RecoverRunCoreAsync` each explicitly capture the result/failure, finalize the activity and dispose it once while retaining the typed effect branch. Their ordinary provider branch remains distinct and unchanged in meaning.

The small public `AgentEffectActivityFinalization` boundary recognizes only supported typed results/failures. It rejects absent or ordinary provider evidence before touching the operation, checks the original bound run/chat and any prior activity stream, and performs no storage, source authority, provider or network work. The existing publication exception has immutable effect facets; a second activity failure preserves the exact original continuation, refusal kind and file-lease uncertainty while independently setting `ActivityDisposalFailed`. It retains diagnostic exceptions internally and keeps public text sanitized. An internal marker preserves the existing public admission exception type/code only after entry into the validated effect-recovery branch; ordinary admissions remain unmarked.

Reachability review included both owning paths in CurrentProfile (synchronous dispatch and awaited completion) and the native recovery owner. Their `WithinOperation` forwarders do not assume ownership. The new synchronous CurrentProfile fixture directly throws from the actual dispatch interface after binding the run; it does not substitute a faulted Task. Its unsubscribed execution relay performs no unrelated proxy event call. The matched actual-owner arrangements retain the physical file-store/lease path. No new per-receipt run reload or provider dispatch was introduced.

The two new Integration Facts cover real committed checkpoint state, incomplete/read-denied/read-unavailable/cancelled/marked-admission knowledge, publication/state observation/disposal combinations, actual Core and CurrentProfile ownership, diagnostic retention and physical file-lease reacquisition. The five existing consumer Facts remain five: the Process Fact now rejects transient retry for the activity carrier, and the native orchestrator Fact exercises actual decorated ownership with one dispatch/disposal, exact continuation, distinct flags and zero completion publication. The existing API Fact adds five activity arrangements, preserving its row count, with 403 denial versus 503 failure and no newly disclosed continuation for denied/unavailable/cancelled cases.

No further blocking source finding was identified in this bounded amendment review. In particular, activity faults do not become file-lease faults, denied read paths do not manufacture continuation details, and Process explicitly retains the original execution with unsafe/non-idempotent retry classification. Parent-owned compilation, exact discovery/execution and static enforcement remain required. The owner-store allocation/write-amplification gate is unchanged.

Parent subsequently identified a final semantic gap: `Confirmed` with `Stop=SnapshotRead` describes a GET observation, potentially of a failed/cancelled run, rather than a newly published waiting checkpoint. The final correction is independently reviewed: both result JSON constructors/factories and the confirmed-cleanup carrier reject that value; public activity admission rejects it before operation property access, and default-interface suspension rejects it before publication. Standalone snapshot serialization and journal/native readback remain supported. The existing result Fact covers both result construction routes and standalone roundtrip. The existing consumer orchestrator Fact now proves helper refusal before any lease access and real-operation refusal without an added event before explicit disposal. No source rows were added.

Reviewed source identity anchors:

| Managed file | SHA256 |
| --- | --- |
| Core `Execution/AgentExecutionActivityCoordinator.cs` | `2CB0463D382CB8F95565DA4AEAA635E901F11D2A74400CFE97881F6A9D427AE4` |
| Core `Execution/AgentExecutionEffectReconciliation.cs` | `627EFDFA89D7AC88A0A19E0B88F05A3ACBF3A9BCB750D1DC8B8452CF7DD1655E` |
| Core `Workspace/AgentFrameworkWorkspaceService.ExecutionFacade.cs` | `393755564799C971262025A636CF9D5A664DC16142D6F5A463F50E40AFF12B5C` |
| AgentFramework `Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs` | `02ECB2BC4E1ECA14697CD598DAD88D0D53623A7F225FCB2FE8573F775A33FBB7` |
| AgentFramework `Services/AgentChatExecutionOrchestrator.cs` | `5214525F6E2778141C47A32B37C182D93BAB83F14F8D7226856CF7B10872D049` |
| Integration `AgentEffectRecoveryActivityFailureTests.cs` | `3A2DC10A55932781E89D3A2E8B8B922A16EE353DB3577A57D2A664C6EE0B19CD` |
| Integration `AgentEffectActivityFaults.cs` | `1F9778836407F1384F43176490508EC795A5A346CB7DCB8D7FD10065947E7F74` |
| Unit `AgentEffectContinuationConsumerTests.cs` | `ABF13B3C1DB808FD1B28061A6843B5161ECA96725A85725706B322EDD7A68C40` |
