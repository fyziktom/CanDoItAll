# C# Testability Plan

- Characterize current security, local/shared validated availability, immediate/recovery behavior and required activity metadata before editing.
- Exercise existing internal production helpers through direct unit-level seams; filesystem tests explicitly use scratch roots and real path/flush behavior. Database projection tests use an isolated leased test DB, not a fake successful query.
- Any extracted helper must be directly instantiable without the large workspace runtime. Retain existing source-specific test seams rather than adding an interface solely for a trivial implementation.
- Count I/O/query/materialization operations to detect a shallow “optimization” that only renames methods. Coupled positive/negative behavior, not counts alone, decides acceptance.
- Existing cancellation/failure/approval orchestration tests and actual store recovery tests cover propagation. Test-only deterministic tools/providers are valid for isolated fault cases; they cannot replace the required live UI conversations and actual provider/tool calls.
- New regression names/case IDs and runtime discovery are frozen in each entry gate. Baseline preservation tests may already pass; do not fabricate failing-first behavior. A performance-bound assertion should fail on the unoptimized baseline, then pass after change; adversarial test variants must distinguish an unsafe shortcut.
- Test classes, filters, source inventory, expected discovery, invalidation and broad-gate rules: `plan/test-selection.md`.
