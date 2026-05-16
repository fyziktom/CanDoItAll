# 17 Temporal Episodic Memory And Replay Scheduler

## Objective

Add explicit temporal episode sequencing and prioritized replay/rehearsal scheduling.

## Inputs

- `architecture/22-temporal-episodic-memory-and-replay.md`
- Existing consolidation, distributed idle compute, probing regression, and process/workflow integration docs.

## Deliverables

- Temporal episode model.
- Episode step model.
- Causal link guidance.
- Replay job kinds and scheduling policy.
- Integration with consolidation and distributed idle compute.

## Acceptance Criteria

- Episodes preserve step order, actors, decisions, artifacts, outcomes, and prediction errors.
- Replay priority uses risk, staleness, surprise, user interest, usefulness, and contradiction pressure.
- Replay output cannot directly promote truth.
- Distributed replay output requires hash/version/policy validation.

## Tests To Add Later

- episode sequence tests,
- causal link tests,
- replay priority tests,
- replay safety tests,
- context-boundary replay regression tests.
