# SB04 — Profile-fenced invocation and provider resolution

Proof tier: **Governed**

## Objective

Compose the existing provider port into a product-only conversation engine that cannot cross database-profile generations.

## Scope

- Implement runtime identity/lease using the canonical database runtime state and the CP0-selected switch/drain coordination seam.
- If DEC-003 confirms the prepared ownership mismatch, extract the narrow provider-profile read and model-capability contracts to AgentFramework.Providers and update existing consumers.
- Move or add the idempotent provider-backed ILlmInvocationPort registration seam in Llm.ProviderRuntime; keep Workflow registration as a consumer.
- Implement product-only profile-fenced ILlmInvocationPort decorator.
- Implement canonical provider profile resolution and server-side model/settings validation through the provider-neutral contracts.
- Resolve allowed thinking efforts per selected provider/model, reject unsupported explicit values,
  preserve provider default versus explicit `None`, and pass the typed override through the lightweight
  provider request.
- Create ILlmChatConversationEngine over LlmConversationService, EF store, and fenced port.
- Check the lease before/after dispatch and in EF store mutations.
- Use definition revision settings on every turn; do not follow current definition revision.

## Expected change surface

- profile/runtime adapters in persistence project
- conversation engine composition
- provider-neutral contract ownership/registration updates where required by DEC-003
- provider validation adapters
- focused unit/integration tests

## Targeted validation

- LlmChatRuntimeFenceTests
- LlmChatProviderResolutionTests
- ProviderRuntimeContractOwnershipTests
- LlmInvocationPortCompositionTests
- LlmChatDefinitionRevisionExecutionTests
- profile switch before, during, and after dispatch cases

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] Profile switch cancels or fails active invocation.
- [x] No assistant message commits after identity generation changes.
- [x] Provider rename does not break stable identity; kind/model mismatch fails.
- [x] Provider/model-specific thinking-effort availability is projected safely and an explicit override reaches dispatch only when supported.
- [x] Suspended/archived definition blocks dispatch.
- [x] LLM Chats resolves provider profiles/capabilities without a project reference to AgentFramework Core or Modules.AgentFramework.
- [x] Workflow and LLM Chat composition share one idempotent provider-backed invocation-port registration seam.
- [x] Generic/global ILlmConversationService remains unregistered.

## Forbidden work

- second profile state singleton
- stale scoped storage root
- fallback to a different provider/model
- provider SDK types in product contracts

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
