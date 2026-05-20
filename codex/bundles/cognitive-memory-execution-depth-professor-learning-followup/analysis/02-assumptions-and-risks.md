# Assumptions and Risks

## Working Assumptions

- The cognitive-memory module may remain deterministic for the first repair pass; live LLM calls are not required for proof.
- Deeper synthesis can be implemented through structured claim normalization, extractive summarization, and provider abstractions before adding generative models.
- Professor/curator input is high-trust but not automatically equivalent to fully internalized memory.
- Existing EF Core persistence can be extended with additive migrations where needed.
- Backward compatibility with existing records should be maintained through migration and versioned algorithm fields.

## Critical Path Risks

- If skills and validators are not hardened first, Codex may again satisfy headings and tests while leaving behavior shallow.
- Pairwise/composite clustering can become expensive; implementation must bound candidate pairs by project, recency, and key-index preselection.
- Overly aggressive curator corrections can damage unrelated memories; ambiguous targeting must stay review-gated.
- Overly trusted professor anchors can dominate memory and suppress later evidence if assimilation/fading is not carefully modeled.
- If recall synthesis hides all provenance, user trust will drop; if it shows all provenance by default, agents/users get overloaded.

## Validation Risks

- Structural bundle validation currently cannot detect semantic shallowness.
- Existing tests often assert that a service produced a record, not that the record is meaningful.
- Browser smoke is not proof for backend cognitive behavior.
- Metric thresholds can be tuned to pass examples without robust behavior unless adversarial tests are included.

## Reopen Triggers

- Any cognitive-memory subbundle closes without at least one adversarial negative test.
- Any subbundle claims semantic synthesis while output still contains internal diagnostic boilerplate as canonical knowledge.
- Any professor anchor can be assimilated using the same direct curator-applied memory record.
- Any aggregate reaches `StrongAccept` from only generated sources, one source family, or unresolved curator conflicts.
- Any final report marks raw user concerns as solved without source-code evidence and behavior proof.
