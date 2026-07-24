# C# Testability Plan

## Characterization

- Preserve the existing integration assertions for project name, selected node, visual targets, path redaction, and prior generated evidence exclusion.
- Record the current integration environment blockers without treating them as product failures.

## Direct Unit Tests

### Launch-context builder

- empty/null input is explicit and safe;
- deterministic hierarchy order and selected-node marker;
- authored visual targets included while screenshots/process-run evidence are excluded;
- native and managed storage paths redacted;
- 40 context-row and 8 visual-asset limits;
- direct output root wins, ancestor fallback works, malformed metadata is ignored;
- all established output-root aliases are emitted.

### Hierarchy selection policy

- reject self and direct duplicate child;
- reject adding an ancestor as a child;
- reject current parent and descendant as reconnect targets;
- terminate safely on cyclic input data;
- accept an unrelated valid candidate.

## Negative/Anti-shallow Proof

- source checkpoint fails if either old owner regains `BuildProjectStructureContextSummary`/output-root policy;
- source checkpoint fails if either production path bypasses the new builder;
- source checkpoint fails if page partial count grows;
- tests instantiate extracted owners without constructing `ProjectStructurePage`.

## Composition/Regression

- build Workbench;
- run targeted new unit tests and architecture checkpoint;
- run page component preservation suites separately;
- attempt the existing integration process-context case with environment-safe execution; record any external host/secret-vault blocker.
