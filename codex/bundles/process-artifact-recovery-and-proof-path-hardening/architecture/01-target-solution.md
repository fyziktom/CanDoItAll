# Target Solution

## Runtime Boundaries

- Dispatch proof validation stays in `ProcessRunAutomationDispatchService`.
- Step transition and reactivation behavior stays in `ProcessRuntimeProgressionPlanner`.
- Process definitions remain generic; no product-specific rule is added.
- Agent rerun directives carry materialization instructions to the source step through existing process rerun infrastructure.

## Data Flow

```mermaid
sequenceDiagram
    participant D as "Downstream step"
    participant R as "Runtime dispatcher"
    participant U as "Upstream source step"
    participant P as "Progression planner"

    R->>D: Load configured artifact inputs
    R->>R: Detect missing upstream artifact input
    R->>D: Block with missing-upstream-artifact reason
    R->>U: Request targeted materialization rerun
    U->>P: Complete after recording artifact
    P->>D: Reopen blocked dependent to Ready
```

## Genericity

The flow is driven by artifact input definitions and step dependencies. It does not inspect product language, framework, route, or domain terms.
