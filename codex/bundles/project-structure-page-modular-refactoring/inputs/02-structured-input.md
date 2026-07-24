# Structured Input

## Raw Notes

- `N001`: `ProjectStructurePage` is difficult to maintain because responsibility remains in one partial-class aggregate.
- `N002`: static behavior suggests independently testable extraction opportunities.
- `N003`: improve at least one meaningful architectural slice using the requested bundle and C# architecture skills.
- `N004`: add proper unit coverage for the extracted behavior.
- `N005`: preserve all existing Project Structure functionality and validate behavior before closure.

## Constraints

- Prefer the smallest cohesive extraction over a broad rewrite.
- Preserve canonical persistence and projection boundaries.
- Keep deterministic algorithms concrete; do not add an interface with one trivial implementation.
- Do not add another partial file or nested handler.
- Keep all changes in the existing Workbench project unless dependency evidence proves a new project is necessary.
- Fail explicitly; do not add fallback behavior.

## Selected Scope

1. Consolidate duplicated process-launch summary/output-root behavior currently owned by both the page and `ProjectStructureProcessNodeService`.
2. Extract project-hierarchy candidate/cycle rules from the page into a directly tested policy.
3. Add architecture assertions preventing duplicate logic or a new page partial from returning.

## Non-goals

- Full page decomposition.
- UI redesign, responsive work, new component composition, or browser-visible behavior changes.
- Changes to canonical project objects, graph persistence, process execution semantics, or party assignment truth.
