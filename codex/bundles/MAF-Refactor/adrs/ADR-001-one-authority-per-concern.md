# ADR-001: One authoritative owner per concern

- Status: Accepted for implementation
- Date: 2026-08-06
- Baseline: `51d9a2f071e9a5f295abac884c8c667328462cc4`

## Context

The current agent path uses several legitimate forms of state: canonical project data, live UI context, chat history, execution runs, tool receipts, approvals, and MAF session state. The architectural problem is not that there are several stores. The problem is that one record or layer sometimes acts as the authority for more than one concern.

The most visible example is `AgentRuntimeTransientContext`, which carries model text, a workspace scope, and opaque typed attachments. A live UI publication can therefore influence both what the model sees and how runtime services are scoped. MAF session state and the application transcript also compete as continuation sources without a versioned adapter envelope.

## Decision

Assign exactly one authoritative owner to each concern:

| Concern | Owner |
|---|---|
| Project, task, process, and other product facts | Owning product module and its canonical persistence |
| Current visible UI facts | Scoped live UI observation registry |
| What one chat thread follows | Conversation context binding service/store |
| Facts supplied to one turn | Immutable turn-context capture |
| Read/mutation authority | Canonically resolved execution-authority snapshot |
| Run state, approvals, receipts, artifacts, and usage | Execution run store |
| Provider/framework continuation payload | Versioned runtime-state envelope owned by the adapter |
| MAF SDK types and mappings | MAF adapter assembly |

No object is allowed to become a universal source of truth. Projections may be cached or persisted, but their authority must remain explicit.

## Consequences

- UI context may request a project scope but cannot grant it.
- A chat can follow the current UI without rewriting an already admitted turn.
- A project switch may retain the transcript while starting a new context epoch and authority snapshot.
- MAF state can be migrated or rejected without changing application truth.
- More records and interfaces are introduced, but each has a narrow lifecycle and test seam.

## Rejected alternatives

### Keep one broad transient context record

Rejected because model context and authorization have different trust, persistence, and continuation requirements.

### Persist the whole live UI snapshot in chat history

Rejected because UI projections and opaque attachments are ephemeral, can be sensitive, and may not be rehydratable.

### Treat the MAF session as canonical conversation state

Rejected because provider/framework state is implementation-specific and cannot own application approvals, authority, or product context.

## Proof

- Canonical-model review classifies every affected record.
- Negative tests prove that a forged UI project/scope cannot grant authority.
- Approval continuation proves that the original turn reference and authority fingerprint remain unchanged.
- Source assertions prove MAF does not own process or product semantics.
