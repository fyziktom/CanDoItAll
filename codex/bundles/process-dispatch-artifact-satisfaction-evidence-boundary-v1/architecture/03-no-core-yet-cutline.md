# No-Core-Yet Cutline

Do not start Process Core in this bundle.

A later Core extraction becomes safer only after:

1. Artifact satisfaction helpers no longer depend directly on large dispatcher internals.
2. Side-effect boundaries are explicitly classified.
3. Required artifact evidence types have stable local snapshots.
4. Candidate hydration/finalizer/dispatch route helpers are stable and well tested.
5. Driver-readiness vocabulary is documented but not implemented.

This bundle should end with an explicit decision:

- `Core still deferred`, or
- `Core preparation candidate identified but not executed`.

Either result is acceptable if evidence-backed.
