# SB02 Semantic and Adversarial Invariants

## SB02-INV-01 — Immediate typed handle and replay from sequence zero

- Invariant ID: `SB02-INV-01`
- Source raw note: agent chat must become visibly responsive before an execution run exists and must emit activity before catalog/provider/session work.
- Expected behavior: `StartSendMessage` synchronously returns an `AgentChatOperationHandle` while context capture is blocked. A reader opened at `StreamSequence.Beginning` (`0`) replays `Accepted` and `CapturingContext`; event envelopes begin at sequence `1` and remain contiguous.
- Disallowed shallow implementation: return a handle only after context/catalog work, or call a cursor value “zero” while treating it as a gap before the first retained event.
- Failing-first test and transcript: the controlled replay-zero shallow mutant fails the matching focused test in `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`.
- Passing test and transcript: `AgentChatExecutionActivityOrchestratorTests.StartSendMessage_returns_before_context_capture_and_exposes_initial_activity`, `PartitionedSequencedStreamTests.CompletedPartition_ReplaysOrderedEventsAndTerminalState`, and `PartitionedSequencedStreamTests.ConcurrentAppends_AssignUniqueContiguousSequences` in `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt`.
- Changed source files/hashes: `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStreamContracts.cs`, `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs`, `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs`; final hashes are recorded in `bundle://proof/SB02/manifest.md`.
- Production assertions: `bundle://proof/SB02/source-assertions.md` SA-01, SA-02, and SA-08.
- Red-team negative case: block context capture, cancel only a reader waiting for sequence `3`, and prove the operation completion remains pending and unaffected until capture is released.
- Downstream dependency check: the independent A2 `Pass` authorizes SB03 and allows
  later SB06 work to rely on the immediate handle, subject to the backend measurement
  gate.
- Production behavior artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`.

## SB02-INV-02 — Isolated ordering with bounded, explicit lifecycle results

- Invariant ID: `SB02-INV-02`
- Source raw note: provide isolated typed pub/sub suitable for a future authorization-aware SSE projection without silent gaps or unbounded state.
- Expected behavior: each partition owns its sequence; readers fan out without cross-partition wakeups; retention overrun returns `SequencedStreamGap`; terminal partitions replay within TTL; later eviction returns `SequencedStreamEvicted`; tombstones expire to `SequencedStreamUnknown`; all-active capacity returns typed rejection without evicting active work; reader cancellation/disposal cannot cancel publishers or other readers.
- Disallowed shallow implementation: a global sequence, silent ring-buffer overwrite, idle eviction of active work, or reader cancellation linked to command cancellation.
- Failing-first test and transcript: the controlled all-active-capacity shallow mutant fails the matching focused test in `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`.
- Passing test and transcript: all eight `PartitionedSequencedStreamTests` plus coordinator replay tests in `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt`.
- Changed source files/hashes: the two `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/` files; final hashes are recorded in the manifest.
- Production assertions: SA-01 through SA-04.
- Red-team negative case: fill every partition with active operations, attempt one more admission, and prove capacity rejection while both original publishers continue; separately overrun retained history and require a typed recoverable gap.
- Downstream dependency check: SB03-SB07 must reopen SB02 if a consumer observes a silent gap, a reused sequence, or active-operation eviction.
- Production behavior artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`.

## SB02-INV-03 — One admitted lifecycle for every workspace execution-run entry

- Invariant ID: `SB02-INV-03`
- Source raw note: operation identity must exist before workspace run creation, every new workspace execution-run entry must require it, and only legacy persisted workspace-run records may omit the original id.
- Expected behavior: raw Core and current-profile public workspace execution methods admit through the coordinator before cold service resolution or dispatch; operation-bound overloads validate profile/workspace/agent/session/id before dependency access; exactly one terminal event wins; approval continuation uses a new operation bound to the same run; `ExecutionRunRecord.InitialActivityOperationId` preserves the initial id while legacy JSON may deserialize it as null.
- Disallowed shallow implementation: mint a typed id at callers but never admit a stream, make the coordinator/lease nullable, or allow a direct service path to reach storage with no lifecycle owner.
- Failing-first test and transcript: all five current-profile direct entries expose Unknown while cold resolution is blocked in `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt`.
- Passing test and transcript: the matching five direct-entry cases pass in `bundle://proof/SB02/transcripts/passing-a2-lifecycle-repair.txt`; the complete repaired semantic suite is 58/58 in `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt`; the five-constructor/static bypass evidence is in `bundle://proof/SB02/transcripts/static-bypass-and-anti-stub.txt`; continuation integration is in `bundle://proof/SB02/transcripts/passing-continuation-targeted-3.txt`.
- Changed source files/hashes: workspace facade/service, execution service, contracts/models, hosting/module composition, and all workspace execution-run caller updates listed in the manifest.
- Production assertions: SA-06, SA-07, SA-10, and SA-11.
- Red-team negative case: submit a default operation id or a lease from another workspace and prove rejection occurs before any store, package, runtime, or capability dependency call.
- Downstream dependency check: SB03 preparation cannot attach to an operation whose admission or run identity is ambiguous.
- Production behavior artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`.
- Explicit boundary: direct `IAgentRuntime` adapters that do not create an Agent
  Framework workspace run, such as `MafWorkflowLlmComponentInvoker`, are not chat
  activity producers in SB02. They must not set a synthetic workspace operation id.
  A future adapter that exposes those invocations through agent UI/SSE must own and
  expose a real typed activity lifecycle rather than relying on the nullable runtime
  correlation option.

## SB02-INV-04 — Profile-authorized reads and profile-pinned dispatch

- Invariant ID: `SB02-INV-04`
- Source raw note: activity must not leak across database profiles/workspaces and scoped readers must remain independent of command cancellation.
- Expected behavior: the module reader authorizes both database profile and exact organization scope, subscribes to profile changes before a second authorization check, and cancels/detaches on switch. Current-profile dispatch resolves and confirms profile/scope, pins the workspace service under its subscription gate, and rejects a mismatched operation before dispatch.
- Disallowed shallow implementation: check the current profile once and resolve the service later, authorize by operation id only, or leave a reader subscribed after disposal/profile change.
- Failing-first test and transcript: an old-profile event queued behind a blocked subscriber crosses the profile switch in `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt`; the controlled profile-pinning mutant also fails the operation-bound dispatch test in `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`.
- Passing test and transcript: the matching profile-generation mailbox fence passes in `bundle://proof/SB02/transcripts/passing-a2-lifecycle-repair.txt`; the complete repaired semantic suite, including the operation-pinning and stale-resolution cases, is 58/58 in `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt`.
- Changed source files/hashes: current-profile reader, current-profile workspace service, workspace factory, and module DI registration; final hashes are recorded in the manifest.
- Production assertions: SA-05, SA-10, and SA-12.
- Red-team negative case: change profile between service resolution and operation property access, and prove the already authorized service remains pinned rather than switching execution to the new profile; separately prove a pending old-profile reader is cancelled.
- Downstream dependency check: any profile leakage reopens SB02 and invalidates all later snapshot/UI proofs.
- Production behavior artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`.

## SB02-INV-05 — Typed context source and version travel with activity

- Invariant ID: `SB02-INV-05`
- Source raw note: the stream must adapt to different module contexts without string topics, hidden object bags, or a second source of truth.
- Expected behavior: after context capture, the operation binds a strongly typed `AgentChatContextSource` plus monotonic snapshot version; later activity envelopes carry that immutable context identity.
- Disallowed shallow implementation: include only agent/session/run ids, copy source kind/id into arbitrary message text, or bind a context object without its version.
- Failing-first test and transcript: removing the production `BindContext` call is killed by the matching focused test in `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`.
- Passing test and transcript: `AgentExecutionActivityCoordinatorTests.Operation_binds_agent_and_typed_context_after_unknown_acceptance` and `AgentChatExecutionActivityOrchestratorTests.StartSendMessage_returns_before_context_capture_and_exposes_initial_activity` in `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt`; the component context-capture regression is in `bundle://proof/SB02/transcripts/passing-component-65.txt`.
- Changed source files/hashes: activity models, coordinator, context invocation factory, and orchestrator; final hashes are recorded in the manifest.
- Production assertions: SA-08.
- Red-team negative case: mutate the live registry after capture begins and prove the dispatched invocation/activity retains the captured source/version, not the later selection.
- Downstream dependency check: SB04 adapters and SB06 UI must not infer context identity from phase/message strings.
- Production behavior artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`.

## SB02-INV-06 — Compatibility notifications cannot govern canonical outcome

- Invariant ID: `SB02-INV-06`
- Source raw note: ephemeral feedback must remain separate from durable history, and throwing or slow compatibility consumers cannot change canonical execution outcome.
- Expected behavior: durable run detail is saved before `ExecutionUpdated`; notification publication uses a bounded mailbox per subscriber and returns without running subscriber code inline; throwing/blocked subscribers are logged or overflowed without suppressing the event sink, runtime entry, later subscribers, activity terminal, or stored completion.
- Disallowed shallow implementation: wrap each callback in `try/catch` but still invoke subscribers sequentially on the producer thread, or queue one unbounded task/mailbox per event.
- Failing-first test and transcript: `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt`.
- Passing test and transcript: `ExecutionUpdatedCompatibilityIsolationTests`, `IsolatedCompatibilityEventDispatcherTests`, and `CurrentProfileAgentExecutionActivityAdmissionTests.Blocked_compatibility_subscriber_does_not_delay_send_or_activity_terminal` are included in the 58/58 repaired semantic suite in `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt`; `AgentFrameworkExecutionRunTrackingIntegrationTests.ExecutionUpdated_subscriber_failure_is_isolated_after_persistence` is included in `bundle://proof/SB02/transcripts/passing-integration-5.txt`.
- Changed source files/hashes: compatibility dispatcher, Core execution/workspace service, current-profile workspace relay, and integration/unit tests; final hashes are recorded in the manifest.
- Production assertions: SA-09 and SA-13.
- Red-team negative case: block the first subscriber, publish persistence/runtime/terminal updates, and prove a later subscriber plus canonical terminal complete before the first subscriber is released.
- Downstream dependency check: SB05 timing and SB06 rendering must not reintroduce awaited UI callbacks.
- Production behavior artifact matrix: `bundle://proof/SB02/producer-consumer-lifecycle.md`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Sequenced typed activity | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/CurrentProfileAgentExecutionActivityReader.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs`; `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` | `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`; `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt` |
| Initial operation/run correlation | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.ExecutionFacade.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`; `bundle://proof/SB02/transcripts/passing-continuation-targeted-3.txt` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/AgentExecutionActivityModels.cs`; `bundle://proof/SB02/transcripts/passing-integration-5.txt` | `bundle://proof/SB02/transcripts/failing-first-a2-lifecycle-repair.txt`; `bundle://proof/SB02/transcripts/passing-a2-lifecycle-repair.txt` |
| Context source/version | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`; `bundle://proof/SB02/transcripts/passing-component-65.txt` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs`; `bundle://proof/SB02/transcripts/passing-focused-unit-52.txt` | `bundle://proof/SB02/transcripts/controlled-shallow-mutant-red-green.txt`; `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` |
| Compatibility `ExecutionUpdated` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`; `bundle://proof/SB02/source-assertions.md` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`; `bundle://proof/SB02/producer-consumer-lifecycle.md` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Events/IsolatedCompatibilityEventDispatcher.cs`; `bundle://proof/SB02/transcripts/passing-integration-5.txt` | `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt`; `bundle://proof/SB02/transcripts/passing-a2-final-validation.txt` |

## Anti-stub conclusion

`bundle://proof/SB02/transcripts/static-bypass-and-anti-stub.txt` records zero
`TODO`, `NotImplementedException`, deliberate `NotSupportedException`, `Task.Delay`, or
`Thread.Sleep` markers in the new production activity-stream surfaces. It also records
zero nullable coordinator/lease bypass patterns. This is supporting evidence only; the
semantic and adversarial tests above carry the behavior claims.
