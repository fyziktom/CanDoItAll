# ADR-004: Approval continuation retains the exact admitted turn context

- Status: Accepted for implementation
- Date: 2026-08-06

## Context

The current implementation binds transient context to an execution run by digest and retains the exact object through approval continuation. It fails closed if that lease is unavailable. This is a strong safety property and must not be lost while improving floating context behavior.

## Decision

Every admitted turn receives an immutable `AgentTurnContextReference` and an in-memory or rehydratable `AgentTurnContextLease`.

The reference contains only safe durable identity:

- capture ID,
- chat/session and context epoch IDs,
- source and observation version,
- model-context digest,
- authority ID and policy fingerprint,
- attachment fingerprints,
- captured timestamp.

The lease contains request-scoped rendered model context and opaque attachments. It is keyed by execution run ID. Approval continuation uses the original reference and lease. It never captures the current UI observation.

A context lease may be rehydrated after restart only when every required attachment has an explicit, canonical, versioned rehydrator and the authority fingerprint can be revalidated without widening access. Otherwise continuation fails closed with an actionable message to start a new turn.

## Consequences

- Switching to Gantt while a Canvas run waits for approval cannot retarget the tool proposal.
- Restart support is explicit rather than accidental.
- Opaque UI payloads are not silently serialized as durable state.
- Context leases remain bounded, observable, and removable at terminal execution.

## Approval decision refinement

Replace the runtime-facing single `bool approved` with a command carrying decisions by proposal/approval ID. A compatibility method may temporarily map one boolean to all currently pending approvals, but new application code must use per-proposal decisions.

## Proof

- Original context digest and authority fingerprint remain identical before and after continuation.
- A newer UI observation is ignored for the old run.
- Mixed approval decisions are mapped by stable IDs.
- Missing or incompatible runtime/context envelopes fail closed.
- Terminal and failed runs release their leases.
