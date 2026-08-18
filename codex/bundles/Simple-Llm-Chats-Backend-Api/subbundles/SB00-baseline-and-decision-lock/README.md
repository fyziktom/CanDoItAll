# SB00 — Baseline characterization and decision lock

Proof tier: **Governed**

## Objective

Revalidate the current development branch, exact owners, canonical identities, provider resolver, API conventions, and focused baseline behavior before production changes.

## Scope

- Load the current SharedInfo architecture and bundle-execution skills.
- Inventory current projects, references, DI registrations, database model registry, migrations, provider profile resolution/capability contracts, ownership of the ILlmInvocationPort registration, API authorization/error mapping, and tests.
- Characterize LlmConversationService, FileLlmConversationStore, ProviderBackedLlmInvocationAdapter, and production non-activation.
- Resolve DEC-001 through DEC-008 in architecture/10-decision-register.md, including the exact owner
  and typed lightweight-request seam for per-model thinking effort.
- Create failing-first or characterization tests only where later behavior lacks a stable seam.

## Expected change surface

- architecture evidence and decision records
- focused baseline tests under tests/Unit or tests/Integration only when needed
- no production source changes

## Targeted validation

- existing LlmConversationServiceTests filter
- existing FileLlmConversationStoreTests filter
- existing lightweight invocation/provider adapter filter
- existing production non-activation/composition characterization

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] All canonical owners and exact source paths recorded, including provider-profile/capability contracts and invocation-port DI ownership.
- [x] DEC-001 through DEC-008 have evidence-backed outcomes.
- [x] Baseline focused tests pass or failures are documented as pre-existing.
- [x] No production code diff.
- [x] CP0 review passes.

## Forbidden work

- new projects or entities
- API routes
- solution-wide test run

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
