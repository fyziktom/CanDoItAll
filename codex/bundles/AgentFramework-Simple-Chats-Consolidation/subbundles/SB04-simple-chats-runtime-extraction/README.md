# SB04 — Simple Chats runtime extraction

## Status

- Completed — focused proof Pass
- Stage: boundaries
- Proof tier: Governed

## Objective

Move provider resolution and conversation execution out of Persistence into a provider-focused Runtime library that consumes Application ports and preserves all profile-fenced execution behavior.

## Owned Requirements

- ASCC-003
- ASCC-006
- ASCC-008
- ASCC-010
- ASCC-014
- ASCC-016
- ASCC-017
- ASCC-018
- ASCC-019
- ASCC-043
- ASCC-044

## Prerequisites

- SB03

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/CanonicalLlmChatProviderResolver.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatInvocationPort.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/AuditedLlmChatStreamingInvocationPort.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/LlmChatsPersistenceServiceCollectionExtensions.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/

## Explicit Non-Goals

- Do not move EF repositories/rows/configurations.
- Do not redesign provider profile/model selection.
- Do not add fallback providers.
- Do not change UI, API, or schema.
- Do not log prompts/messages/secrets/raw provider payloads.

## Implementation Steps

1. Create SimpleChats.Runtime with Core/Application and existing generic provider runtime references only.
2. Characterize provider resolver, completed/streaming invocation, auditing, cancellation, timeouts, and conversation engine behavior.
3. Move canonical profile/model resolver and invocation adapters/decorators.
4. Move conversation engine/execution orchestration; inject repository/evidence/profile-fence/lease behavior through Application ports.
5. Leave EF-backed fresh-scope, generation fence, lease/heartbeat, and commit-fence implementations in Persistence for SB05.
6. Replace manual CreateConversationEngine construction with explicit Runtime registration/factory composition; avoid service location.
7. Preserve Chat-purpose filtering, enabled/capability validation, reasoning settings, streaming fallback semantics, and explicit errors.
8. Add registration cardinality and forbidden EF/AppDbContext reference tests.
9. Remove moved runtime code from the old Persistence project.
10. Capture masked logs and direct owner proof.

## Acceptance Criteria

- [ ] Runtime has zero EF/AppDbContext/Persistence concrete references.
- [ ] Resolver and engine test without a database.
- [ ] Profile/failure/streaming/cancellation behavior is unchanged.
- [ ] Runtime registered exactly once.
- [ ] Old Persistence no longer constructs providers/engine.

## Validation Depth

- Proof tier: Governed.
- Critical foundation: yes; Persistence and all main/floating execution behavior depend on it.

Governed critical extraction with failing-first runtime tests, direct negative dependency proof, old-owner shrink, registration graph, and architecture review.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Integration.slnx

Required:

- LlmChatProviderRuntimeTests
- LlmChatWholeUseCaseProfileScopeTests
- LlmChatBackendCompositionTests
- LlmChatOperationTests
- LlmChatTransactionalConcurrencyIntegrationTests

Add exact cases RuntimeHasNoEfReference, DisabledChatProfileFailsExplicitly, StreamingCancellationPreservesAttemptEvidence.

Expected discovery: non-zero for every selector and all three new cases.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden. Reopen on provider resolver/engine/invocation/profile/cancellation/DI/logging behavior.

## UI Composition Contract

No visual change. Existing main and floating Simple Chat presentation must remain compatible through Application contracts.

## C# Architecture Impact

Separates provider/runtime responsibility from Persistence and removes manual mixed construction.

## Boundary Ownership

Runtime owns provider execution. Persistence owns database implementations. Application owns the seam.

## Dependency Direction

Runtime -> Application/Core/generic LLM/Providers/Usage; never Runtime -> Persistence/EF/Agent module.

## Pattern Decision

Ports/adapters and decorator pattern for audited invocation; explicit composition factory only where runtime construction is genuinely variable.

## Testability Contract

Resolver/adapters/engine construct from fakes and deterministic provider profiles without AppDbContext or host startup.

## Partial Class Policy

No partial extraction. Engine helpers become top-level collaborators only when cohesive and directly tested.

## Architecture Proof Required

Before/after project graph, source ownership, direct tests, EF forbidden-reference test, old Persistence shrink, DI cardinality, cycles, architecture gate.

## Progression Gate

- Runtime extraction green unlocks SB05 persistence relocation.

## Reopen Triggers

- Runtime acquires EF/Persistence reference;
- profile fence becomes permissive;
- provider fallback introduced;
- audit attempt semantics drift;
- duplicate runtime/hosted registration.

## Covered Inputs

- Raw request: keep provider-related Simple Chat execution in MAF and out of the Agent module/Persistence dumping ground.
- Requirements ASCC-003, ASCC-006, ASCC-008, ASCC-010, ASCC-014, ASCC-016–019, ASCC-043–044.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/CanonicalLlmChatProviderResolver.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/LlmChatsPersistenceServiceCollectionExtensions.cs

## Deliverables

- EF-free SimpleChats.Runtime, explicit Runtime composition, and removed provider/engine ownership from old Persistence.

## Dependency Impact

- SB05 persistence cutover, SB07 UI behavior, and SB11 E2E depend on identical profile/stream/cancel/audit semantics.

## Acceptance Checklist

- All Acceptance Criteria above pass with no EF reference, fallback provider, sensitive log, or duplicate registration.

## Proof Required

- proof/SB04/manifest.md, failing/passing resolver/engine/stream tests, DI/reference/source guards, old-owner shrink/hashes, architecture gate.
