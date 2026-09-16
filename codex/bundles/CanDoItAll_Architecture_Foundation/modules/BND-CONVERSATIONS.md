# BND-CONVERSATIONS — Shared conversation presentation

**Target owner:** Shared conversation presentation

Neutral message rendering, composers and floating windows. Session instances, focus, overlay order and subscription lifecycle.

**Repository discovery location:** `None`

**Primary sources:** SRC-006, SRC-009, MAP-03, MAP-07

## Provided responsibilities

- Reusable message rendering, composer intent, floating-window layout, focus, overlay order and session lifecycle.
- UI-only state/intent contracts for different product-specific adapters.

## Required capabilities

- Sanitized immutable presentation state from an Agents or Simple Chats adapter, not a domain runtime service.
- Shell navigation/JS handles and explicit view lifetime. Attachment selection emits intent rather than writing files or calling providers.

## Forbidden shortcuts

- Execution, approvals, tool availability, transcript retention and API access belong to the respective product adapter.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: preserve floating agents over Structure/Gantt without references from neutral UI to those domains.
- ESTIMATE: another conversation product can supply another adapter without adding its business rules to shared components.

## Persistence

Owns no business transcript or run persistence. Local drafts/window/view state are UI-owned and scope-bound; persisted UI state must not masquerade as business authority. The product adapter supplies data and cancellation policy.

## Recorded capabilities

- **FEAT-101** [USER; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] The floating agent remains over different work views and supports contextual work.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.


## Proof and unresolved questions

Relevant planned scenarios: QA-001, QA-002, QA-005, QA-006, QA-071, QA-072, QA-095, QA-098, QA-100, QA-134.

Related gaps: GAP-018, GAP-029.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
