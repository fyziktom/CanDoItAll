# Approval State Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Running
    Running --> ApprovalSurfaced: model returns approval-required tool call
    ApprovalSurfaced --> MafStateStored: 1.15 binding snapshots request in AgentSession.StateBag
    MafStateStored --> AppRecordStored: application stores display/audit record with stable request and call IDs
    AppRecordStored --> SessionSerialized: serialize exact AgentSession
    SessionSerialized --> Waiting

    Waiting --> NativeRestore: state version is 1.15+
    NativeRestore --> DecisionValidated: restore exact session and current pending snapshot
    DecisionValidated --> BoundResponse: atomic decision is bound by MAF to the original request
    BoundResponse --> ExecutedOnce: MAF rebinds original tool call
    ExecutedOnce --> Completed

    Waiting --> LegacyDetected: pre-upgrade request lacks native 1.15 binding state
    LegacyDetected --> Reissue: drain or re-run to surface a native 1.15 request
    Reissue --> ApprovalSurfaced

    Waiting --> Rejected: missing session, unstable ID, unknown ID, stale state
    Rejected --> [*]
    Completed --> [*]
```

## Invariants

- A user decision never carries authoritative tool name or arguments.
- The server-held model-originated request is the authority.
- Every pending record retains the stable MAF request ID and call ID.
- A decision applies atomically to the current server-held pending snapshot and is
  consumed once by the restored MAF binding state.
- Unknown, duplicate, stale, cross-session, or modified approvals do not execute.
- Process-local cache loss cannot remove the persistent security boundary.
- No compatibility bridge reconstructs a request from client data or private framework
  JSON.
- Session scrubbing cannot remove approval binding state.
