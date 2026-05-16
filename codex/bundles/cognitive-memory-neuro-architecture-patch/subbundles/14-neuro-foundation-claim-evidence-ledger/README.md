# 14 Neuro Foundation: Claim/Evidence Ledger And Mutation Authority

## Objective

Add the architecture foundation for atomic claims, evidence anchors, entity/context binding, and mutation authority.

## Inputs

- `architecture/20-claim-evidence-belief-ledger.md`
- `architecture/21-schema-entity-context-binding.md`
- `contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs`
- Existing `architecture/03-memory-taxonomy-and-data-model.md`
- Existing `architecture/10-security-governance-and-provenance.md`

## Deliverables

- Architecture update for claim/evidence/belief ledger.
- Evidence anchor model with source spans/structured paths/quote hashes.
- Context frame and entity registry model.
- Mutation authority design.
- Updated requirements, traceability, and acceptance criteria.

## Implementation Rules For Future Code

- Do not expose public direct upsert operations for authoritative memory.
- Do not promote generated summaries without evidence anchors.
- Do not silently merge claims with different context frames.
- Keep source code comments in English.

## Acceptance Criteria

- Claim-level contradiction can be represented independently from memory item summary.
- Every authoritative claim operation has evidence or an explicit generated/draft reason.
- Mutation operations are idempotent and audited.
- Context boundaries can prevent semantically similar memory substitution.

## Tests To Add Later

- claim support/attack tests,
- source anchor tests,
- mutation idempotency tests,
- production/test Docker context boundary tests,
- generated-summary promotion rejection tests.
