# Assumptions And Risks

## Working Assumptions

- P0/P1 refactors are accepted as the current baseline and should not be reverted.
- Existing persistence and review/mutation concepts should be extended rather than replaced wholesale.
- Qdrant/vector projection is optional for some tests, but the architecture should support semantic clustering when a vector provider exists.
- Dreaming should be explicit and auditable first; automatic background execution can be introduced later.
- Generated synthesis may use an LLM provider, but deterministic test fakes must cover the behavior.

## Critical Path Risks

- Cluster schema changes may create migration complexity and must be introduced with EF model tests.
- Over-aggressive clustering can merge unrelated memories and corrupt context.
- Under-aggressive clustering can leave the system as shallow as today.
- LLM-based synthesis can hallucinate unless every generated statement is grounded and validated.
- Recall synthesis can hide important uncertainty if it removes all diagnostic context without reference-on-demand support.
- Fixing SideContext promotion may change existing recall test expectations and must be handled deliberately.

## Validation Risks

- A small happy-path corpus can make dreaming appear correct while missing contradictions, duplicate claims, and temporal supersession.
- Tests that only assert record counts will not prove quality.
- A dream run that finishes quickly can still pass count-based tests unless quality metrics are required.
- Redaction/access tests must be repeated for synthesized output, not only raw context packs.
- Browser tests can pass UI rendering while API/DTO provenance is incomplete.

## Reopen Triggers

- New P2/P3 refactors change consolidation or recall contracts after this bundle is prepared.
- The team decides to introduce economic memory governance before this base quality loop is complete.
- The implementation chooses an external summarization/LLM provider without deterministic test fakes.
- Aggregate memories are activated without claim-level source maps.
- Any regression allows restricted source text into concise synthesized answers.
