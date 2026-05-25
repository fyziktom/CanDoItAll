# Target Solution

The target solution is a typed process-runtime governance layer. Process definitions persist operation contracts, dispatch emits typed execution metadata, tool policy enforces operation authorization, artifact projection uses typed lineage and explicit mappings, finalization validates storage-backed artifact content, and post-step auditing detects policy bypasses before downstream steps proceed.

```mermaid
flowchart TD
    Definition["Process definition"]
    Contract["Persisted step operation contract"]
    Lint["Definition lint and publish/start gate"]
    Dispatch["Process dispatch"]
    Metadata["Execution metadata"]
    Policy["Operation-aware tool policy"]
    Executor["Agent, workflow, or subprocess executor"]
    Projection["Typed artifact projection adapter"]
    Finalizer["Process-owned finalizer"]
    Audit["Runtime invariant audit"]
    Recovery["Typed recovery and escalation"]
    Next["Next step activation"]

    Definition --> Contract
    Contract --> Lint
    Lint --> Dispatch
    Dispatch --> Metadata
    Metadata --> Policy
    Policy --> Executor
    Executor --> Projection
    Projection --> Finalizer
    Finalizer --> Audit
    Audit --> Recovery
    Recovery --> Next
```

## Design Constraints

- Keep process concepts generic across business planning, legal review, manufacturing QA, HR, incident response, software delivery, and research.
- Keep workflows below processes. Workflow state may satisfy a process role only through explicit process-owned mapping and validation.
- Keep PostgreSQL-only runtime assumptions. Do not add SQLite runtime paths or SQLite migrations.
- Prefer persisted typed state over prompt text, keyword parsing, or bounded display keys.
