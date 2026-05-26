# Target runtime architecture v7

```mermaid
flowchart TD
    Definition[Process Definition]
    Contract[Persisted Step Operation Contract]
    APISchema[Processes API / Tool Schemas]
    SkillDocs[Skills + Documentation]
    Lint[Strict Lint / Readiness Gate]
    Dispatch[Process Dispatch]
    Metadata[Execution Metadata]
    Ledger[Grounded Target Ledger]
    ToolPolicy[Operation + Ledger Aware Tool Policy]
    Executor[Agent / Workflow / Subprocess / Human]
    ArtifactService[Unified Artifact Validation Service]
    Finalizer[Process-Owned Completion Finalizer]
    Transition[Manual/API Transition]
    BlockState[Typed Block + Recovery State]
    Health[Runtime Health / Diagnostics]
    Next[Next Step Activation]

    Definition --> Contract
    Contract --> APISchema
    Contract --> SkillDocs
    APISchema --> Lint
    SkillDocs --> Lint
    Lint --> Dispatch
    Dispatch --> Metadata
    Metadata --> Ledger
    Ledger --> ToolPolicy
    ToolPolicy --> Executor
    Executor --> ArtifactService
    ArtifactService --> Finalizer
    ArtifactService --> Transition
    Finalizer --> BlockState
    Transition --> BlockState
    BlockState --> Health
    Health --> Next
```

## Core principles

- Processes own lifecycle, artifacts, transitions, recovery, and governance.
- Workflows are below Processes and can only satisfy process roles through explicit process-owned mapping.
- Public API/tool models must be as authoritative as runtime models.
- Skills/docs are not optional; agent correctness depends on them.
- Text inference is fallback only.
- Typed governance metadata must be inspectable in UI, API, tests, and audit.
