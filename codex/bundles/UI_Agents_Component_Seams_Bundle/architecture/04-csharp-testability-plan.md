# Testability and proof layers

Use the [behavior matrix](../requirements/02-behavior-preservation-matrix.md) and [test inventory](../inventories/02-test-impact-and-classification.md). Every new responsibility needs a meaningful test at its boundary; no artificial test quota.

1. Pure state/policy tests cover typed section/selection/target transitions, identity protection, normalization and permission mapping.
2. Real operation tests construct production use cases normally with deterministic external ports and exercise load, mutation, partial failure, conflict and refresh outcomes.
3. Adapter/composition tests exercise actual registrations and real adapters against the repository's isolated fixture style. Validate operations, not only that DI can resolve an interface.
4. Component tests render actual catalog/editor/page with typed inputs and fake external operations. Exercise real conditional children and nested dialogs for owned scenarios.
5. Real-host browser proof verifies production composition, existing routes, dialog lifetime, selection/chat readiness and overlays.

A fake editor controller proves the view contract, not the actual persistence adapter. A dependency metadata test proves only the types it inspects; closure also needs subtree/reference/asset evidence.

Replace reflection/uninitialized setup during SB02–SB05 alongside the seam it tests. SB06 checks the complete map and migrates the adjacent Workflows button case. Shared helpers, including AgentsHomePageTestExtensions, are in scope.

Async safeguards need delayed success and failure after a newer target/reset/close, plus two concurrent editor instances. Persistence tests distinguish pre-commit failure, conflict, successful commit, failed refresh, and failed UI callback. Required scenario gaps block the owning phase rather than becoming an SB07 surprise.
