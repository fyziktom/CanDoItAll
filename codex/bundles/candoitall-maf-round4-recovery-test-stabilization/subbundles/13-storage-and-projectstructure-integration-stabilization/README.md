# 13 — Storage and ProjectStructure Integration Stabilization


## Problem

Codex reported storage/project-structure integration failures. These suites need stronger isolation and deterministic cleanup.

## Tasks

1. Audit failing storage and project-structure tests.
2. Ensure each test has an isolated temp root, profile, database, and artifact directory.
3. Remove assumptions about global environment state, current user path, or fixed repository locations.
4. Ensure cleanup is robust even after test failure.
5. Make tests serial only where shared external state is unavoidable.

## Acceptance criteria

- Storage/project-structure tests pass under the default green gate or are explicitly categorized if heavy.
- No test writes to shared repo/global directories unexpectedly.
- Failure output includes temp root paths and relevant diagnostics.

