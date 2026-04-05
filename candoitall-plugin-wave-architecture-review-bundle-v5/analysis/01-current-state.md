# Current State

## Bottom Line

The codebase is **healthier than before**, but it is **not yet architecturally ready for the next plugin wave**.

The improvements are real:

- canonical party ownership moved in the right direction
- node-scoped assignment operations use a typed wrapper at the boundary
- delete/move compensation paths exist and are tested
- hierarchy cycle protection exists
- pure view state is already separated from node persistence

But the remaining issues are still foundational:

- Workbench still persists synchronized projection nodes from other modules into canonical-looking tables.
- The universal node record still mixes too many concerns.
- Kind semantics are still spread across enums, subtype strings, catalog definitions, and editor switches.
- Reclassification still mutates in place without explicit semantic history.
- Plugin architecture is still too enum/switch oriented for the upcoming integration wave.

## Readiness Verdict

- **Continue normal local development:** allowed
- **Start large integration/plugin wave:** not yet
