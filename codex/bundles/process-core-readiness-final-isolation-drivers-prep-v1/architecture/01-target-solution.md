# Target Solution

The detailed target-boundary document is preserved in `bundle://architecture/01-target-boundaries.md`.

## Target Boundaries

- Reduce route adapter callbacks into `ProcessRunAutomationDispatchService` where route services can own module-local collaborators.
- Split candidate hydration into explicit application-local collaborators for query/read, artifact input preparation, assignment, direct-agent binding, recovery lookup, run availability, and candidate assembly.
- Move pre-execution materialization and start-transition planning into explicit module-local services.
- Separate subprocess orchestration from artifact projection persistence and reduce dispatcher nested alias use.
- Isolate finalizer and failure closure models behind module-local contracts.
- Burn down dispatcher static wrappers that only forward to existing rule classes.
- End with Core/driver readiness documentation only; no production Core or driver APIs are created in this bundle.
