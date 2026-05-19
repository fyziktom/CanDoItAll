# Target Solution

## Beta Validation Architecture

```mermaid
architecture-beta
    group host(server)[CanDoItAll Web Host]
    group docker(server)[Docker Services]
    group memory(server)[Cognitive Memory]
    group proof(server)[Bundle Proof]

    service api(server)[Cognitive Memory v1 API] in host
    service ui(server)[Blazor Operator UI] in host
    service db(database)[PostgreSQL AppDbContext Profile] in docker
    service qdrant(database)[Qdrant gRPC 6334] in docker
    service rebuild(server)[Projection Rebuild Service] in memory
    service recall(server)[Recall Orchestrator] in memory
    service report(server)[Execution Report] in proof

    api:R -- L:rebuild
    api:R -- L:recall
    ui:R -- L:api
    rebuild:B -- T:db
    recall:B -- T:db
    rebuild:R -- L:qdrant
    recall:R -- L:qdrant
    api:B -- T:report
    ui:B -- T:report
```

## Validation Flow

```mermaid
flowchart TD
    Start["Prepared bundle validator"] --> Audit["Audit P0/P1 beta gates"]
    Audit --> Infra["Verify Docker Qdrant and PostgreSQL"]
    Infra --> App["Start app with Qdrant config"]
    App --> Seed["Ensure durable memory/projection inputs"]
    Seed --> Rebuild["Run v1 projection rebuild"]
    Rebuild --> Qdrant{"Projected points in Qdrant?"}
    Qdrant -- "No" --> Fix["Fix blocker and rerun"]
    Fix --> Rebuild
    Qdrant -- "Yes" --> Recall["Run recall/vector validation"]
    Recall --> Browser["Browser health/audit proof"]
    Browser --> Docs["Update docs and roadmap to true stage"]
    Docs --> Final["Tests, build, diff check, completed validator"]
```

## Boundaries

- Durable memory, claims, evidence, review decisions, traces, and runs stay in the active database profile.
- Qdrant proof must show projection health and searchable vector behavior; it is not source of truth.
- Validation commands may inspect Qdrant collection state, but they must not mutate durable memory outside app/API-controlled setup or explicit test fixture code.
- If code changes are needed, they must preserve API compatibility and keep provider failures explicit.

