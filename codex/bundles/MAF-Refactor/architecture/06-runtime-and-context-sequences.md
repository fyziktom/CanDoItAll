# Runtime and context sequences

## New floating turn

```mermaid
sequenceDiagram
    participant UI as Active UI surface
    participant Obs as UI observation registry
    participant Ctx as Turn context capture
    participant Aff as Conversation context service
    participant Auth as Authority resolver
    participant Exec as Execution coordinator
    participant MAF as MAF adapter

    UI->>Obs: Publish atomic observation v42
    UI->>Ctx: User sends message
    Ctx->>Obs: Capture strict snapshot v42
    Ctx->>Aff: Resolve previous binding
    Aff-->>Ctx: Project X / Canvas / rev 3
    Ctx->>Ctx: Classify Canvas -> Gantt
    Ctx->>Auth: Resolve canonical authority
    Auth-->>Ctx: Project X authority snapshot
    Ctx-->>Exec: Immutable turn context + request
    Exec->>Exec: Persist context reference and authority fingerprint
    Exec->>MAF: Runtime-neutral execution request
    MAF-->>Exec: Runtime-neutral result
```

## UI changes during a run

```mermaid
sequenceDiagram
    participant UI
    participant Obs as Observation registry
    participant Run as Active execution
    participant Chat as Floating chat

    Run->>Run: Bound to observation v41 / Canvas
    UI->>Obs: Publish v42 / Gantt
    Obs-->>Chat: ContextChanged event
    Chat->>Chat: Show "next turn: Gantt"
    Note over Run: No mutation of v41
    Run-->>Chat: Complete using v41
```

## Approval continuation

```mermaid
sequenceDiagram
    participant UI
    participant Exec as Execution coordinator
    participant Lease as Original turn-context lease
    participant Auth as Persisted authority reference
    participant MAF as MAF adapter

    UI->>Exec: Decide proposal(s)
    Exec->>Lease: Resolve by execution run id and digest
    Lease-->>Exec: Original model context
    Exec->>Auth: Validate original authority compatibility
    Auth-->>Exec: Original authority retained
    Exec->>MAF: Continue original runtime state
    Note over UI,MAF: Current UI surface is not recaptured
```

## Cross-project transition

```mermaid
sequenceDiagram
    participant UI
    participant Obs as Observation registry
    participant Aff as Conversation binding
    participant Auth as Authority resolver
    participant Exec as Execution coordinator

    UI->>Obs: Publish Project Y observation
    Exec->>Obs: Capture next turn
    Exec->>Aff: Compare previous Project X binding
    Aff-->>Exec: SourceEntityChanged
    Exec->>Auth: Resolve Project Y access
    alt authorized
        Auth-->>Exec: Project Y authority
        Exec->>Aff: Commit binding revision after admission
    else denied
        Auth-->>Exec: Denied
        Exec-->>UI: Context access error
    end
```

## Target adapter boundary

```mermaid
flowchart LR
    UI[UI observation contributors] --> CAP[Turn context capture]
    CAN[Canonical product state] --> AUTH[Authority resolver]
    AFF[Conversation binding] --> CAP
    AUTH --> APP[Execution coordinator]
    CAP --> APP
    APP --> PORT[Runtime-neutral execution ports]
    PORT --> MAF[MAF adapter]
    MAF --> SDK[Microsoft Agent Framework]
    PROC[Processes recovery policy] --> APP
    WB[Workbench product tools and context] --> APP
```

## Ordinary workflow LLM invocation

```mermaid
sequenceDiagram
    participant WF as Workflow executor
    participant LLM as ILlmInvocationPort
    participant PR as Provider runtime
    participant DR as Provider chat driver

    WF->>WF: Build immutable ordered messages + response format
    WF->>LLM: InvokeAsync(request)
    Note over WF,LLM: No AgentDefinition, agent session, tools, memory, UI context, or workspace authority
    LLM->>PR: Resolve provider runtime handle and dispatch request
    PR->>DR: CompleteChatAsync(provider request)
    DR-->>PR: Provider-neutral completion + usage
    PR-->>LLM: Dispatch result
    LLM-->>WF: LLM result + finish/usage/evidence
    WF->>WF: Apply workflow-owned schema validation and usage projection
```

## Future ordinary LLM conversation

```mermaid
sequenceDiagram
    participant UI as Ordinary chat UI
    participant CONV as LLM conversation service
    participant STORE as Conversation store
    participant LLM as ILlmInvocationPort
    participant PR as Provider runtime

    UI->>CONV: Send user turn
    CONV->>STORE: Load transcript and conversation policy
    STORE-->>CONV: Ordered transcript + provider/model choice
    CONV->>LLM: Stateless invocation request
    LLM->>PR: One provider dispatch
    PR-->>LLM: Completion + usage
    LLM-->>CONV: Stateless result
    CONV->>STORE: Atomically persist user/assistant turn and usage
    CONV-->>UI: Updated conversation
    Note over CONV,LLM: Transcript and compaction stay above the stateless port; this is not an agent run
```

## Target inference boundaries

```mermaid
flowchart LR
    WF[Workflow LLM executor] --> LA[Llm.Abstractions]
    CHAT[Future ordinary-chat application] --> LA
    LA --> LPR[Provider-backed LLM adapter]
    LPR --> PR[Existing provider runtime/pool]
    PR --> DR[Provider chat drivers]

    APP[Agent execution coordinator] --> RA[Runtime.Abstractions]
    RA --> MAF[MAF agent adapter]

    LA -. forbidden .-> MAF
    CHAT -. forbidden .-> MAF
```
