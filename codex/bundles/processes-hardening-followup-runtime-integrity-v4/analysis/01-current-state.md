# Current State

Phase3 materially improved the process runtime. The code now has:

- explicit operation concepts in dispatch metadata
- product mutation metadata passed through AgentFramework execution metadata
- tool policy checks for product mutation boundaries
- workflow/subprocess finalizer coverage improvements
- manager recovery lineage markers
- upstream materialization reactivation code
- process definition linting and editor display

However, the implementation remains fragile in several high-impact areas:

- Some lifecycle operations happen before EF changes are saved.
- Some lineage is encoded in bounded free-text fields.
- Some safety decisions are derived from path/summary/provenance text rather than typed sources.
- Script tools can mutate targets indirectly without tool policy seeing the actual side effect.
- Artifact validation does not reliably resolve storage-backed content.
- Workflow/subprocess mapping is still heuristic.
- Disposition routing can still mask missing own artifacts.
- Lint defaults and UI display are not strong enough to guarantee safe process definitions.
