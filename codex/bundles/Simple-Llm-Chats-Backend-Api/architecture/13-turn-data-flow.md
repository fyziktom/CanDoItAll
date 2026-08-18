# Turn data flow

## New turn

```mermaid
sequenceDiagram
    participant Client
    participant API as Web API
    participant App as LlmChatOperationApplicationService
    participant Repo as PostgreSQL repositories
    participant Engine as Product conversation engine
    participant Generic as LlmConversationService
    participant Port as Profile-fenced invocation port
    participant Provider as Existing ILlmInvocationPort

    Client->>API: POST turn(operationId, expectedRevision, text)
    API->>App: typed command
    App->>Repo: admit/reuse operation + request fingerprint
    alt matching operation already exists
        App-->>API: existing/reconciled state
    else new operation
        App->>App: create runtime lease and operation scope
        App->>Engine: send(turnId = operationId)
        Engine->>Generic: SendAsync
        Generic->>Repo: CAS admit pending user entry
        Generic->>Port: InvokeAsync
        Port->>Port: verify profile identity
        Port->>Port: resolve and validate per-model thinking effort
        Port->>Provider: existing stateless dispatch with typed override envelope
        Provider-->>Port: result or typed failure
        Port->>Repo: append immutable invocation audit with requested/effective effort
        Port->>Port: verify profile identity
        Port-->>Generic: result/failure
        Generic->>Repo: CAS assistant completion or exact compensation
        Generic-->>Engine: transcript result
        Engine-->>App: result
        App->>Repo: finalize operation
        App-->>API: operation + assistant result
    end
    API-->>Client: 200/202/typed error
```

## Profile switch during dispatch

1. database switch notification cancels the runtime lease;
2. provider call receives the linked cancellation token where possible;
3. the fenced port verifies identity after dispatch even if the provider ignored cancellation;
4. it records known usage/outcome;
5. it throws a typed fence failure;
6. generic service compensates the exact pending user entry;
7. transcript store checks the lease before mutation;
8. operation becomes failed/cancelled/recovery-required according to persisted evidence.

No result is silently committed into a newer profile.

## Crash reconciliation

Because operation ID equals turn ID, recovery can distinguish:

- completed assistant entry;
- active pending user entry;
- fully compensated/no-entry failure;
- conflicting foreign turn.

This identity is the core recovery invariant.
