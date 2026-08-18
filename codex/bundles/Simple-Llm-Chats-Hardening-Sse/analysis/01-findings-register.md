# Findings register

All Critical and High findings are blockers for the next UI wave. Medium findings owned by this bundle
must also close before FINAL because they affect the external API contract.

## F-001 — Critical: Conversation unit of work is not one transaction

**Claim:** The product application service opens an ILlmChatUnitOfWork, but EfLlmConversationStore creates a separate AppDbContext and transaction.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationApplicationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatUnitOfWork.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Conversations/EfLlmConversationStore.cs`

**Why it matters:** Create can leave orphan product/transcript state; rename can commit two different titles.

**Owning work unit:** SB01

## F-002 — Critical: Conversation title and transcript metadata have duplicate writable truth

**Claim:** Product and transcript rows both carry mutable conversation metadata.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Entities/LlmChatPersistenceRows.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/EntityConfigurations/LlmChatConversationConfigurations.cs`

**Why it matters:** Independent commits can diverge and every read must guess which row is authoritative.

**Owning work unit:** SB01

## F-003 — Critical: Turn admission and evidence are split across commits

**Claim:** ProfileFencedLlmConversationStore persists the conversation change, then invokes evidence callbacks through another unit of work.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/ProfileFencedLlmConversationStore.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEvidenceService.cs`

**Why it matters:** A crash or evidence failure can leave an active transcript turn without matching operation evidence.

**Owning work unit:** SB02

## F-004 — Critical: Assistant commit and terminal operation state are not atomic

**Claim:** The provider result can be committed to the transcript before operation success is committed.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`

**Why it matters:** A retry/recovery path may see a completed answer and a nonterminal or failed operation.

**Owning work unit:** SB02

## F-005 — Critical: Compensation exhaustion is swallowed

**Claim:** Compensation retries are bounded but exhaustion returns without establishing a durable RecoveryRequired outcome.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** A Failed or Cancelled operation can retain a live active turn that normal recovery refuses to abandon.

**Owning work unit:** SB02

## F-006 — Critical: Committed cancellation can still become success

**Claim:** CompleteTranscript accepts CancellationRequested and finalization does not re-check durable cancellation in the same transaction.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats/Operations/LlmChatOperation.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** The state machine can report Succeeded after the caller durably cancelled before final commit.

**Owning work unit:** SB02

## F-007 — High: Idempotent replay is gated by later mutable lifecycle state

**Claim:** Definition/conversation eligibility is checked before resolving an existing operation and its fingerprint.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** A successful request may stop replaying after the definition is suspended or the conversation archived.

**Owning work unit:** SB02

## F-008 — Critical: Profile fence does not cover the complete use case

**Claim:** The runtime lease surrounds conversation engine/provider work, while metadata reads/writes happen outside it.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/DatabaseProfileLlmChatRuntimeLease.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** One request can combine metadata, provider, and commits from different profile generations.

**Owning work unit:** SB03

## F-009 — High: Running ownership and cancellation are process-local

**Claim:** The cancellation/running registry is in memory and reconciliation uses local presence as liveness evidence.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationCancellationRegistry.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** A second app instance can recover a live operation owned by the first; cross-instance cancellation is ineffective.

**Owning work unit:** SB04

## F-010 — High: HTTP request lifetime owns paid execution

**Claim:** The send endpoint awaits provider execution inline using the request cancellation token.

**Evidence:**
- `src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** Client disconnects and slow local LLMs produce accidental cancellation and poor external-client behavior.

**Owning work unit:** SB04

## F-011 — High: Attempt audit does not represent actual attempts consistently

**Claim:** The audit wrapper records ordinal 1 while the provider adapter may perform more than one dispatch attempt.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatInvocationPort.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`

**Why it matters:** Usage, retry, and recovery evidence cannot reconstruct the actual dispatch sequence.

**Owning work unit:** SB02

## F-012 — High: Timeout and cancellation reduce differently before and after restart

**Claim:** DeadlineExceeded is written as Cancelled in invocation audit but handled as a failed deadline by the direct request path.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatInvocationPort.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`

**Why it matters:** The same durable evidence can yield a different terminal operation state after reconciliation.

**Owning work unit:** SB02

## F-013 — High: Conversation and transcript reads are unbounded or in-memory paged

**Claim:** The EF conversation store materializes the full transcript and list paths build summaries from full documents.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Conversations/EfLlmConversationStore.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatConversationRepository.cs`
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationApplicationService.cs`

**Why it matters:** Long-running enterprise chats and chatbot traffic create memory, latency, and N+1 growth.

**Owning work unit:** SB05

## F-014 — High: Archive can race active work

**Claim:** Archive does not atomically reject an active turn or nonterminal operation.

**Evidence:**
- `src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationApplicationService.cs`

**Why it matters:** A conversation may be archived while a provider answer is being admitted or finalized.

**Owning work unit:** SB02

## F-015 — High: Provider contracts and drivers have no true streaming path

**Claim:** IProviderChatCompletionDriver exposes only CompleteChatAsync; OpenAI/Azure use completed HTTP responses and Ollama sends stream=false.

**Evidence:**
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OllamaProviderDriver.cs`

**Why it matters:** An SSE endpoint alone could only poll or fake progressive output.

**Owning work unit:** SB07

## F-016 — Medium: HTTP origin is caller-controlled and dedicated LLM scopes are missing

**Claim:** Conversation creation accepts an Origin field and the API family lacks distinct read/manage/execute policy names.

**Evidence:**
- `src/App/CanDoItAll.Web/Api/LlmChatsApi.cs`
- `src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs`

**Why it matters:** Audit provenance can be spoofed and remotely enabled authorization is less granular than required for enterprise/external clients.

**Owning work unit:** SB10

## F-017 — High: Committed closure and branch provenance are not release-ready

**Claim:** The feature branch is behind development, has no workflow run at its head, and the original bundle records a red stable gate.

**Evidence:**
- `codex/bundles/Simple-Llm-Chats-Backend-Api/EXECUTION-PROGRESS.md`
- `codex/bundles/Simple-Llm-Chats-Backend-Api/reviews/FINAL-MERGE-DECISION.md`
- `codex/bundles/Simple-Llm-Chats-Backend-Api/subbundles/SB11-final-regression-and-release-gate/SESSION-HANDOFF.md`

**Why it matters:** The next wave would rely on stale or incomplete proof and an unsynchronized baseline.

**Owning work unit:** SB00
