# SB06 Semantic Invariants

- Invariant ID: SB06-INV-001
- Source raw note: Preserve user-managed workflow definitions and do not overwrite definitions that lack the managed seed marker.
- Expected behavior: Example seeding creates repository-owned examples while leaving user-owned definitions untouched even when names match template names.
- Disallowed shallow implementation: Matching only by display name, overwriting descriptions without checking the managed marker, or replacing user-owned definitions during seed.
- Failing-first test: N/A - process hardening added preservation coverage for existing seeding behavior without changing UI files.
- Passing test: Component tests for example seed creation and user definition preservation passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`.
- Production assertions: Existing `WorkflowExampleCatalogSeedService` uses the configured seed marker as ownership evidence; no Razor UI files changed.
- Red-team negative case: A user-owned workflow with a template-derived name but no seed marker keeps its original description after seeding.
- Downstream dependency check: Component test verifies the example catalog count still matches the template pack after preservation.
