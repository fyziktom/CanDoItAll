# Structured input

## Raw-note IDs

| Raw note ID | Exact intent |
| --- | --- |
| `U001` | Review the new Process module, not just the surrounding solution. |
| `U002` | Check for duplicated logic across modules as well as inside the Process module. |
| `U003` | Focus on architecture, overloaded functions, long files, maintainability, modularity, unit-testability, canonicality, performance, and DB conflict risk. |
| `U004` | Produce a detailed execution-grade bundle for Codex so implementation and testing are not skipped. |
| `U005` | Make the subbundles detailed and precise enough for Codex to execute safely. |
| `U006` | Use in-repo bundle examples as a standard and improve on them. |
| `U007` | Add recurring architecture review subbundles that can force corrective work before the next phase continues. |
| `U008` | Deliver the bundle as a zip artifact. |

## Normalized objectives

1. Build an initiative-grade bundle from the actual repository, not from stale assumptions.
2. Drive correction of the Process module in an order that protects canonicality first.
3. Insert repeated architecture review gates with explicit corrective behavior.
4. Give Codex exact source references, tests, proof expectations, and stop rules.
5. Make cross-module consolidation visible, but do not over-centralize domain-specific behavior.

## Hard constraints

- The bundle must be zip-deliverable.
- The bundle must be structured like the in-repo bundle ecosystem.
- The bundle must be more detailed than the standard baseline.
- The bundle must not assume successful build/tests until those are actually run on the target machine.
- The bundle must give Codex precise guidance and validation expectations.

## Assumptions

- The current repository structure and bundle conventions remain valid execution targets.
- `ProcessesService` may remain the public façade during refactor if that reduces caller churn.
- Both SQLite and PostgreSQL support must be preserved.
- Existing test projects remain the main proof surfaces.

## Risks visible at preparation time

- The dependency-model repair could be weakened by compatibility shortcuts.
- Persistence and concurrency fixes can easily regress current runtime/editor behavior.
- Over-eager helper extraction can increase coupling instead of reducing it.
- UI decomposition can accidentally move domain logic into the component layer.
- The bundle can look complete while still missing real execution proof unless the validators and execution report are enforced.
