# Regression Scenarios

## Cluster Regression Seeds

- Three unrelated memories in one project with the same access/risk and month must not produce an aggregate-ready cluster based only on project/month/access.
- Two memories with same source system/type but unrelated topics must not produce an aggregate-ready cluster based only on source topology.
- Two memories with shared semantic topic, shared entity, and independent source items should produce an eligible cluster.
- Contradictory relation cluster should be routed to review/failure-learning, not approved aggregate application.

## Dream Regression Seeds

- Mixed-topic cluster candidate must be rejected or require review.
- Good cluster candidate must produce synthesized claims, not copied source summaries.
- Candidate with only machine-generated aggregate sources must not auto-approve.
- Candidate depending on a memory superseded by curator correction must not apply.

## Curator Regression Seeds

- Czech new knowledge phrase: `Zapamatuj si, že ...` should capture as new knowledge or require explicit UI capture, not be ignored silently.
- Czech correction phrase: `To není správně, ve skutečnosti ...` should capture as correction when target is explicit.
- Correction with three included recall memories and no explicit target must not supersede all three.
- Explicit target correction should supersede/refine only the selected memory/claim.

## Recall Synthesis Regression Seeds

- Agent-facing brief should not show internal scores or references by default.
- Reference-on-demand should return statement -> aggregate claim -> source memory -> source item/evidence anchor lineage where applicable.
