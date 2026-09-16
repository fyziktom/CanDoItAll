# BND-SIMPLECHATS — Simple Chats: independent ordinary LLM conversations

**Target owner:** Simple Chats

Owns definitions, conversations, durable operations, leases and journals. Sharing UI or floating shells does not activate agent tools.

**Repository discovery location:** `None`

**Primary sources:** SRC-006, SRC-009

## Provided responsibilities

- Ordinary conversation definitions, durable turn admission/status/replay, transcript/retention and operation identity.
- Product-specific adapters for workspace/floating shells and authorized HTTP APIs.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Provider-neutral LLM/model contracts and provider-runtime adapters; persistence and owner admission/leases.
- Neutral conversation presentation; any explicit Prompt Gallery/report adapters live outside product core.

## Forbidden shortcuts

- Implicit Project Structure context is not delivered according to documentation. An explicit bounded-input adapter is a future feature, not a required refactor. Closing a surface is not operation cancellation.
- Adding tools, agent execution, implicit context, or transcript administration merely because an HR agent can edit a definition.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: preserve ordinary no-tools/no-implicit-project-context behavior and non-idempotent conversation creation versus retry-safe turns.
- ESTIMATED FROM DOCUMENTATION: explicit project-context adapters and deployment/channel/human-handoff models belong to separate later features. Characterize existing reporting hooks first.
- Requested HR/CRM-specialist definition administration via an external adapter reusing existing definition API. Keep all product/persistence dependencies on agent tooling absent; do not grant transcript access.

## Persistence

Owned conversation/operation/event journals, leases and PostgreSQL persistence stay isolated from Agent execution. SSE replays committed journal events. Do not merge transcript identities or retention rules with Collaboration or Agents.

## Recorded capabilities

- **FEAT-098** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Simple Chats admission commits user message and operation; durable leases drive dispatch, completion commits, and committed SSE journal replay.
- **FEAT-099** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Simple Chats has floating/workspace UI without agent tools; closing the surface does not cancel an admitted turn.
- **FEAT-100** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] HTTP conversation creation is documented as non-idempotent; turn admission uses a caller operation ID.
- **FEAT-114** [CODE; OBSERVED_CODE_NOT_RUNTIME_PROVEN] Existing definition API supports create, update, status, get, and paged query with concurrency tokens for changes.
- **FEAT-115** [USER; REQUIRED_EXTENSION_NOT_DELIVERED] HR administration of Simple Chats definitions through a new owner-backed, approved runtime adapter.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-007 — Provider execution profile lease:** caller.
- **CON-009 — Usage evidence and allocation query:** caller.
- **CON-033 — Prompt search/version retrieval:** caller.
- **CON-045 — Post-commit invalidation/live progress:** caller.
- **CON-049 — Ordinary Simple Chats operations:** declaration owner, caller, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-061 — Simple Chats definition discovery for administrators:** declaration owner, implementation/integration boundary.
- **CON-062 — Simple Chats definition administration:** declaration owner, implementation/integration boundary.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.

## Proof and unresolved questions

Relevant planned scenarios: QA-042, QA-051, QA-071, QA-072, QA-073, QA-115, QA-129, QA-130, QA-131, QA-132, QA-133, QA-134, QA-135, QA-136, QA-137, QA-138, QA-155, QA-161, QA-163.

Related gaps: GAP-018, GAP-029, GAP-034, GAP-035.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
