# Target Architecture

```mermaid
flowchart TD
    A[Process definition version]
    B[Persisted step operation contract]
    C[Typed target grounding ledger]
    D[Dispatch metadata]
    E[Tool policy operation authorizer]
    F[Executor: Agent / Workflow / Subprocess]
    G[Projection adapter]
    H[Artifact identity + storage validation]
    I[Unified completion validator]
    J[Typed block/recovery router]
    K[Process health + invariant audit]

    A --> B
    A --> C
    B --> D
    C --> D
    D --> E
    E --> F
    F --> G
    G --> H
    H --> I
    I --> J
    J --> K
```

## Design

- Tool policy must consume a typed ledger, not just alias strings.
- Completion validation must be a shared service used by automated finalizer and manual/API transitions.
- Artifact identity must not depend on bounded display reference strings.
- Recovery must be typed and executable, not just a reason string and a list of options.
- Legacy heuristics remain only as compatibility fallback with visible warnings.
