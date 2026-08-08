# ADR-003: UI observation is not execution authority

- Status: Accepted for implementation
- Date: 2026-08-06

## Context

The current context scope carries per-agent access and a `WorkspaceScopeDescriptor`. The invocation factory propagates that scope into execution metadata and transient context. This is convenient, but it places a UI projection too close to authorization and workspace service construction.

## Decision

Split turn preparation into two independently testable operations:

1. Capture a live `AgentUiObservationSnapshot`.
2. Resolve an `AgentExecutionAuthoritySnapshot` from canonical authorization and product services.

The observation provides a requested source identity and view facts. The authority resolver independently validates:

- database profile and generation,
- agent identity and lifecycle,
- current principal/local authority,
- product/module access,
- allowed operations and capability scope,
- external target grants,
- read versus mutation rights,
- workspace execution scope.

The runtime receives workspace scope only from the authority snapshot. A mismatch between observation source/scope and resolved authority fails before execution admission.

The existing `AgentChatContextAgentAccess` may remain temporarily as an early visibility/readiness hint. It must not be used as the final mutation authority.

## Consequences

- A visible Gantt view can inform the model without granting mutation rights.
- A forged `projectId`, route parameter, model message, or workflow payload cannot select authority.
- Read-only agents can observe a project while mutation tools remain unavailable.
- Authority is persisted as a safe identity/fingerprint, not as a full live authorization object.

## Rejected alternatives

### Trust the module publication because it is application-generated

Rejected because UI state and canonical authorization have different lifetimes and failure modes. A stale or incorrectly wired component must not widen runtime permissions.

### Revalidate only inside each tool

Rejected as the sole mechanism. Tool-level authorization remains mandatory, but admission also needs a coherent authority snapshot to construct the correct workspace/runtime services and tool catalog.

## Proof

- Negative test: publication claims mutation but canonical resolver returns read-only; no mutation tool is attached.
- Negative test: publication names Project Y while the authorized source is Project X; admission fails.
- Negative test: workflow payload contains a project ID; the direct LLM port does not acquire project authority.
- Integration test: tool-level policy and admission authority agree on scope identity.
