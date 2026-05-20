# Assumptions and Risks

## Working Assumptions

- The extracted repo under `/mnt/data/review/CanDoItAll-development` represents the current implementation submitted by the implementation agent.
- Existing database entities can be extended through migrations, but the implementation agent must preserve backward compatibility where practical.
- The previous quality-foundation bundle remains the intended design direction, but this follow-up may supersede weak implementation details.
- Curator/professor mode is allowed to trust the user as source of truth, but it must still model target ambiguity and evidence lineage.
- Existing source provenance and access/redaction policy constraints are non-negotiable.

## Critical Path Risks

- If broad low-signal clusters remain aggregate-eligible, later dream validation and recall synthesis will appear to work while building meaningless aggregate memories.
- If curator correction targeting remains broad, one professor correction can accidentally stale or supersede unrelated memories from the same recall context.
- If professor assertions are applied immediately without assimilation state, the memory will not know whether it has internalized the lesson or is merely leaning on the latest trusted turn.
- If aggregate application remains overconfident, weak generated summaries will pollute future recall and dream runs with high-confidence machine-generated memories.
- If recall synthesis remains first-line grouping, agents will still receive noisy context rather than a useful memory brief.

## Validation Risks

- Existing unit tests currently assert weak behavior, especially broad key family cluster creation, so tests must be rewritten rather than only extended.
- In-memory/unit tests can prove state transitions but not UI ambiguity, target selection, or agent-facing brief readability; component/browser proof is required for curator UI changes.
- LLM-based semantic validation can become nondeterministic; the bundle requires deterministic heuristics and fixture-driven proof first, with LLM/provider validation optional and policy-gated.
- The implementation agent may treat curator trust as a reason to skip review gates; this bundle explicitly forbids broad unreviewed supersedes.

## Reopen Triggers

- Reopen clustering if any default dream run can select a cluster whose only strong signal is project scope, month, access/risk, or source item type.
- Reopen dreaming if aggregate canonical text remains a copied list of source summaries without synthesized claims, uncertainty, contradictions, or lineage.
- Reopen validation if a mixed-topic aggregate candidate can be approved without human review.
- Reopen curator targeting if a correction using a recall trace supersedes more than the explicitly targeted memory/claim unless a review item or confirmation is created.
- Reopen curator assimilation if professor anchors never change state, never influence cluster/dream scoring, or never retire/fade after stable derived memories exist.
- Reopen recall synthesis if the default agent-facing output includes internal scores/references by default or cannot expand references on demand.
