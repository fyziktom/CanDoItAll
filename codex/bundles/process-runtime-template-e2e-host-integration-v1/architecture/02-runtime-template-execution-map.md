# Runtime Template Execution Map

The next implementation must prove real process template execution across these surfaces:

| Surface | Required proof |
| --- | --- |
| Template catalog | Inventory exact template keys, including software development, Blazor/.NET, business analysis, and multi-team development if present. |
| Global UI | `/processes` template selection, launch plan, run selection, run detail. |
| Project UI | `/projects/{projectId}/processes` with project-scoped context. |
| Project structure | Node-linked process start and run-output navigation. |
| Process runtime | persisted run, steps, outbox entry, dispatch claim, finalizer, terminal status. |
| Artifacts | expected artifacts, managed content readback, lineage, projection into project/workbench when applicable. |
| Manager diagnostics | verification/dry-run readback tied to process run and step. |
| Scheduler/workflow origin | read-only job lifecycle and process start provenance. |

If multi-team development is missing or renamed, the implementation must not silently skip it. It must create an inventory artifact and either restore the template, map the new key, or create an explicit follow-up blocker.
