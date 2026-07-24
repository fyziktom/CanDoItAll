# Pattern Selection Records

## Immutable Builder

- Problem force: the same deterministic multi-step projection is duplicated in two production owners and returns related summary/output-root facts.
- Selected pattern: a top-level concrete builder returning an immutable result.
- Rejected simpler option: another private static helper would remain tied to one old owner and would not create a direct test seam.
- Rejected heavier option: interface/DI factory; no alternate implementation, external dependency, or lifecycle exists.
- New types: `ProjectStructureProcessLaunchContextBuilder` and its immutable result.
- Dependency direction: both callers point toward the builder; the builder never points back.
- Proof: direct unit tests, both-caller source assertion, duplicate-method absence.

## Policy

- Problem force: hierarchy graph eligibility is pure domain policy embedded in UI orchestration.
- Selected pattern: top-level static policy with explicit attach and reconnect operations.
- Rejected simpler option: leaving private static methods in the page preserves responsibility concentration.
- Rejected heavier option: strategy/factory; algorithms are closed operations, not interchangeable implementations.
- New type: `ProjectStructureProjectHierarchySelectionPolicy`.
- Proof: direct self/duplicate/cycle/current-parent/valid-candidate tests and page delegation assertion.
