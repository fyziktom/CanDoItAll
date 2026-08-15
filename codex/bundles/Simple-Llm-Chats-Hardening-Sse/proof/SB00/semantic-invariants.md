# SB00 semantic invariants

| Invariant | Positive evidence | Negative evidence | Result |
|---|---|---|---|
| Development synchronization is real, not asserted from stale metadata. | `git merge-base HEAD origin/development` resolves to `eb6be3ea38075b442d24976655f5c45ac08bd6b5`; development is an ancestor of `5522880cbf3101ed54c216ab74cac3b8ff2bade0`. | The pre-sync merge base was `c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee`, so stale provenance would not satisfy the assertion. | Pass |
| Every prior stable failure is classified against both synchronized heads. | Two identical focused filters execute 19 cases; the inventory has 19 concrete rows. | Counts or a feature-only rerun could not distinguish baseline from branch behavior. | Pass |
| CP0 carries no active feature regression. | Four former Agent/Workflow regressions pass on both heads; feature has only seven failures that also fail on development. | A feature-only failure or unclassified case would be BranchInduced or Unresolved and block CP0. | Pass |
| Broad stable proof remains single-shot. | SB00 executes only the exact prior-failure slice through a three-project reproduction solution. | No `CanDoItAll.slnx` test command appears in the SB00 proof manifest. | Pass |
| Architecture inventory is tied to the synchronized source. | CodeAnalytics snapshot `snap-20260814234111-c9c24513` has zero cycles, diagnostics, open questions, or Error findings. | The earlier unscoped snapshot was discarded as noisy and is not used as acceptance evidence. | Pass |
