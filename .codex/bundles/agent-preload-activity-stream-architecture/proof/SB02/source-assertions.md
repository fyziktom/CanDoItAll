# SB02 Production Source Assertions

These assertions describe the live working tree inspected for the SB02 proof pack.
Line anchors are evidence-capture anchors; the LF-normalized SHA-256 manifest binds the
reviewed source state.

## SA-01 — Generic sequence ownership and replay-zero cursor

- `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStreamContracts.cs:18` defines `StreamSequence.Beginning` as `0`.
- `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs:23` owns typed admission.
- `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs:83` owns terminal append.
- `repo://src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs:114` creates independent readers.
- The read path special-cases `Beginning` when the earliest retained sequence is `First`; a fresh replay from zero returns the first event rather than a gap. A real retention overrun returns `SequencedStreamGap` at line 213.

## SA-02 — Bounded partitions, terminal retention, and tombstones

`PartitionedSequencedStreamPolicy` strongly types maximum partitions, events per
partition, terminal partitions, terminal retention, tombstones, and tombstone retention.
The stream evicts only terminal partitions, rejects capacity when all partitions are
active, records a typed eviction reason, bounds tombstones, and expires them to unknown.
No publisher consults reader cancellation.

## SA-03 — Per-partition ordering and reader independence

The stream assigns `PartitionState.NextSequence` while holding that partition's gate.
Every reader owns a cursor, read semaphore, and disposal token. Reader cancellation and
disposal wake only that reader; producer state and other readers remain independent.

## SA-04 — Agent lifecycle owns terminal state

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs:108` admits a typed stream and publishes `Accepted`.
- The operation exposes typed context/run binding at lines 274 and 296.
- Terminal methods begin at lines 365-387 and converge on the single stream completion at line 482.
- Transition rules reject backward or semantically invalid progress and terminal outcomes.

## SA-05 — Authorized reader is scoped over canonical singleton state

- Module registration creates the singleton stream/coordinator at `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs:128-136` and a scoped authorized reader at line 137.
- Standalone hosting registers the same real stream/coordinator/read pipeline at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs:83-99`.
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/CurrentProfileAgentExecutionActivityReader.cs:35-48` authorizes, subscribes to profile change, then authorizes again before opening the inner reader.
- The reader requires exact current database profile plus exact organization scope and cancels/detaches when the profile changes or the reader is disposed.

CodeAnalytics can directly identify the scoped reader registration but reports
factory-based singleton registrations as only partially interpreted. The executable DI
tests, not the analyzer alone, prove singleton/scoped identity.

## SA-06 — Every Core execution entry admits or validates a lease

`repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/AgentFrameworkWorkspaceService.ExecutionFacade.cs`
routes all five public mutation families through `ExecuteNewActivityOperationAsync`:

- `ExecuteRunAsync` at line 39;
- `ExecuteSameSourceRunAsync` at line 67;
- `ContinueExecutionRunAsync` at line 99;
- `SendMessageAsync` at line 160;
- `RespondToPendingApprovalsAsync` at line 204.

The common admission owner begins at line 261. Operation-bound overloads delegate only
after the execution service validates workspace scope, agent, session, and operation id.
Coordinator and workspace identity are required constructor parameters; there is no
nullable production fallback.

## SA-07 — Typed operation/run correlation is durable but activity remains ephemeral

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs:483` makes the chat run options id required.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs:498` makes the execution request id required.
- `ExecutionRunRecord.InitialActivityOperationId` at line 464 is nullable only for legacy persisted JSON.
- `AgentRuntimeExecutionOptions.ActivityOperationId` at line 377 is transient runtime correlation.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:1037-1040` rejects request/lease mismatch and line 2074 carries the exact id into runtime options.

No activity envelope is written into durable execution history by the stream.

## SA-08 — Immediate handle and typed captured context

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs:23` and line 49 expose immediate send/approval handles.
- The operation is admitted before context capture; `Task.Yield` prevents synchronous downstream work from withholding the handle.
- Lines 126-128 bind `context.Scope.Source` and `context.Version` through the typed `BindContext` API.
- Lines 216-229 resolve the current profile twice around organization-scope resolution and reject a changed/mismatched profile before admission.

The current UI still calls the task-returning compatibility methods. Consuming the
handle/reader in Blazor is SB06, not a hidden SB02 claim.

## SA-09 — Compatibility publication cannot run callbacks inline

- Canonical run detail is saved at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:62`.
- The compatibility notification is enqueued at line 75.
- The execution event sink remains an independently awaited canonical step at line 76.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Events/IsolatedCompatibilityEventDispatcher.cs:84` publishes to independent subscriber registrations.
- Each subscriber has a bounded mailbox; line 335 schedules work through `ThreadPool.UnsafeQueueUserWorkItem`.
- Lines 111-125 advance the dispatcher generation, while lines 198-210 and 246-254
  reject older publisher snapshots and discard queued older-generation notifications.
- Handler failure and mailbox overflow reporting are caught and logged; failure reporting itself cannot throw back into the producer.

The integration test intentionally awaits the asynchronous compatibility notification
independently. It asserts canonical event-sink publication precedes runtime entry, not an
invalid ordering claim between queued callbacks and canonical work.

## SA-10 — Current-profile execution is profile-pinned

`repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`
resolves and double-confirms only the current profile/scope identity, admits direct
calls under `executionSubscriptionGate`, and only then constructs the cold workspace
service. It confirms the same identity after construction, validates operation access
against the pinned tuple, and dispatches the operation-bound call before releasing the
gate. Resolution or dispatch failure terminalizes and disposes the admitted operation.
Profile change detaches the old compatibility relay and advances the dispatcher's
generation before attaching the new service, so queued old-profile notifications are
discarded. There is no retry or silent fallback to another profile.

## SA-11 — All manual workspace constructions are explicitly wired

The exact static audit in
`bundle://proof/SB02/transcripts/static-bypass-and-anti-stub.txt` found five direct
`new AgentFrameworkWorkspaceService(...)` calls:

- production `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs:123`;
- four test constructions.

All five supply a non-null coordinator and explicit workspace identity. The scan found
zero nullable coordinator/lease declarations and zero `activityOperation: null` calls.

## SA-12 — Workspace-service ownership is bounded

The module factory caches manually constructed services by
`AgentExecutionActivityWorkspaceIdentity` (profile plus scope). Disposal snapshots and
clears the cache under lock, disposes outside the lock, aggregates failures, rejects
reuse, and is idempotent. Component proof is in
`AgentFrameworkWorkspaceFactoryDisposalTests`.

## SA-13 — Existing compatibility consumers remain explicit

Current production subscribers are:

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/FloatingAgentChatCoordinator.cs:44`;
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs:162`;
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor:412`;
- the current-profile relay at `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs:1025-1035`.

These are compatibility consumers, not typed-stream consumers. The production typed
reader facade exists now; UI projection remains SB06 and external SSE remains out of
scope.

## Typed caller inventory

Production callers mint a strongly typed id before entering an admitted facade at:

- `repo://src/App/CanDoItAll.Web/Api/AgentsApi.cs`;
- `repo://src/App/CanDoItAll.Web/Program.cs`;
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`;
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Hosting/ScenarioHarnessSupport.cs`;
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/Hr/HrAgentProcessReviewService.cs`;
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`;
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`;
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRunNarrativeGenerator.cs`;
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepExecutor.cs`.

The orchestrator allocates its own id because it returns the corresponding handle.
