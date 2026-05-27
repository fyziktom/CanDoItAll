# Invariants

1. A MAF 1.6 feature is considered adopted only if production code uses it and a test/source assertion proves the usage.
2. A deferred MAF feature must have an explicit reason and safe fallback.
3. Artifact dedupe must not bind the wrong step or expectation.
4. Strict required narrative artifacts must be content-backed unless explicitly configured otherwise.
5. Read-model satisfaction cannot be more optimistic than finalizer validation.
6. Operator approval artifacts cannot substitute for required deliverables/briefs without explicit mapping.
7. The next live test must start from a clean live-run profile, not pre-seeded transitions/artifacts.
