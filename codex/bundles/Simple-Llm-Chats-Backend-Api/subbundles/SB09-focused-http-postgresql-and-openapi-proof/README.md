# SB09 — Focused HTTP, PostgreSQL, and OpenAPI proof

Proof tier: **Governed**

## Objective

Prove the API end to end against the real host and PostgreSQL before documentation/final regression.

## Scope

- Create one coherent real-host API integration class or tightly bounded family.
- Exercise definition lifecycle, revision pinning, conversation creation, two turns, pagination, rename/archive, idempotent replay, conflict, cancellation, provider failure, recovery, and profile switch.
- Exercise two models on one provider with different effort capabilities, provider default, explicit
  `None`, supported override dispatch/audit, and unsupported override rejection.
- Verify PostgreSQL rows, database-transfer round-trip, and no file-store output.
- Validate OpenAPI operation names/status schemas.
- Run CP2 review.
- Run only focused filters.

## Expected change surface

- focused Integration tests and test support
- OpenAPI snapshot/assertions as repository conventions allow
- CP2 proof

## Targeted validation

- LlmChatsApiIntegrationTests focused family
- LlmChatsDatabaseTransferIntegrationTests focused round-trip
- MigrationBootstrapIntegrationTests focused case
- architecture/test-policy scripts

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [ ] Real HTTP host and PostgreSQL path pass.
- [ ] No test bypasses API for the primary behavior under proof.
- [ ] Definition edit does not change existing conversation revision.
- [ ] Real HTTP/PostgreSQL proof covers model-specific effort options, validation, persisted revision, dispatch, and audit.
- [ ] Idempotent replay does not add a message or invocation record.
- [ ] Canonical database-transfer export/import round-trip preserves the complete LLM Chat graph.
- [ ] CP2 passes and unlocks SB10.

## Forbidden work

- entire Integration project
- Playwright
- live external provider dependency

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
