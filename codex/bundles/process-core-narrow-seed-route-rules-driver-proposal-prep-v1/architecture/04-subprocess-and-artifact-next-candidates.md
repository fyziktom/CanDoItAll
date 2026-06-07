# Subprocess And Artifact Next Candidates

## Subprocess

Candidate for future narrow Core only:

- pure status mapping,
- pure parent transition reason builders,
- pure artifact source mapping rules.

Do not move:

- child run observation,
- capability gap DB reads,
- projection persistence,
- gap journals,
- finalizer calls.

## Artifact expectations

Candidate for future narrow Core only:

- immutable expectation snapshots,
- pure expectation matching,
- pure satisfaction classification.

Do not move:

- storage placement,
- filesystem reads/writes,
- workspace path resolution,
- provider-native artifact import,
- recovery lineage persistence,
- validation orchestration with side effects.
