# SB02 — Application ports and use cases

Proof tier: **Governed**

## Objective

Define and implement product application behavior without EF or Web dependencies.

## Scope

- Add repositories, unit-of-work, provider resolver, runtime lease, conversation engine, cancellation, audit, and clock ports.
- Make the provider resolver expose safe provider/model option projections and validate the typed
  thinking-effort override without duplicating provider capability rules.
- Implement definition lifecycle and append-only revision use cases.
- Implement conversation creation command that pins an exact revision.
- Implement list/detail/rename/archive application commands and bounded query contracts.
- Document future context, attachment, policy, and deployment boundaries; add no speculative interface without a current consumer.
- Use repository canonical Result/Error conventions selected in SB00.

## Expected change surface

- Application contracts and services in Modules.LlmChats
- no persistence or HTTP code
- unit tests using direct fakes

## Targeted validation

- LlmChatDefinitionServiceTests
- LlmChatConversationApplicationServiceTests
- LlmChatApplicationBoundaryTests

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] Application services have explicit dependencies and no IServiceProvider.
- [x] Archived/suspended lifecycle is enforced.
- [x] Conversation creation resolves and pins a definition revision.
- [x] Provider credentials and SDK types do not enter commands/results.
- [x] Future extension boundaries are documented and no speculative unused interface or registration is added.

## Forbidden work

- generic manager/facade with unrelated methods
- EF entities
- Web DTOs
- concrete Project Structure or channel adapters

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
