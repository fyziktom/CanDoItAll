# SB06 — Composition and backend checkpoint

Proof tier: **Governed**

## Objective

Activate the backend deliberately and prove the complete non-HTTP feature block before API work.

## Scope

- Register domain and persistence services through explicit extension methods.
- Register AppDbContext model assembly by current composition convention.
- Register product ILlmChatConversationEngine and database-transfer handler; keep generic service dormant.
- Prove LLM Chat backend composition does not rely on the Workflows module to register ILlmInvocationPort.
- Add startup/DI validation tests.
- Run CP1 architecture review and only focused backend test union.
- Update module and architecture documentation.

## Expected change surface

- Composition registration
- module/persistence service collection extensions
- DI/architecture tests
- CP1 proof

## Targeted validation

- focused DI composition tests
- focused SB01–SB05 unit union
- focused SB03–SB05 PostgreSQL union
- focused database-transfer registration/round-trip test
- architecture boundary script

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] All backend services resolve in a real profile scope.
- [x] No UI project or agent execution dependency exists.
- [x] File store is not selected by production.
- [x] Provider-backed invocation composition resolves independently of workflow-node registration.
- [x] Profile lifecycle subscriptions dispose correctly.
- [x] The LLM Chats database-transfer handler resolves through the canonical transfer registry.
- [x] CP1 passes and unlocks SB07.

## Forbidden work

- HTTP routes
- solution-wide test
- temporary service locator

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
