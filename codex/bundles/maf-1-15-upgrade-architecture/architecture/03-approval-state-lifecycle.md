# Approval State Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Running
    Running --> ApprovalSurfaced: model returns approval-required tool call
    ApprovalSurfaced --> MafStateStored: 1.15 binding snapshots request in AgentSession.StateBag
    MafStateStored --> AppRecordStored: application stores display/audit record and fingerprint
    AppRecordStored --> SessionSerialized: serialize exact AgentSession
    SessionSerialized --> Waiting

    Waiting --> NativeRestore: state version is 1.15+
    NativeRestore --> DecisionValidated: restore session and match application record
    DecisionValidated --> BoundResponse: decision references exact approval ID
    BoundResponse --> ExecutedOnce: MAF rebinds original tool call
    ExecutedOnce --> Completed

    Waiting --> LegacyDetected: state is 1.13 or binding state absent
    LegacyDetected --> Reissue: preferred
    Reissue --> ApprovalSurfaced

    LegacyDetected --> TrustedBridge: temporary feature flag only
    TrustedBridge --> FingerprintValidated
    FingerprintValidated --> RequestAndResponseReplay
    RequestAndResponseReplay --> ExecutedOnce

    Waiting --> Rejected: missing session, bad fingerprint, unknown ID, stale state
    Rejected --> [*]
    Completed --> [*]
```

## Invariants

- A user decision never carries authoritative tool name or arguments.
- The server-held model-originated request is the authority.
- One approval ID maps to one exact tool call and one session/run.
- A decision is consumed once.
- Unknown, duplicate, stale, cross-session, or modified approvals do not execute.
- Process-local cache loss cannot remove the persistent security boundary.
- The compatibility bridge cannot accept arbitrary client-supplied request objects.
- Session scrubbing cannot remove approval binding state.
