# BND-STORAGE — Storage and FileTools

**Target owner:** Storage and FileTools

Storage catalogs, logical locators, managed bytes/revisions, path containment/provenance and authorized browsing/opening.

**Repository discovery location:** `None`

**Primary sources:** SRC-004, SRC-005, SRC-008, MAP-11

## Provided responsibilities

- Managed object/revision/placement and content streams; staging/finalization/cleanup according to driver.
- Provenance-aware deletion, reference-aware eligibility and authorized file scopes; desktop opening is a separate host capability.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Security secret resolution for a specific driver; control-plane current profile/root identity.
- Owner-issued bindings/scopes and deletion eligibility, not arbitrary agent-authored paths.

## Forbidden shortcuts

- Resource metadata, native Structure nodes and run artifact manifests are not one storage business entity. FileTools exposes only owner-issued scopes.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: preserve read/create/file/preview/delete outcomes and compensation for partial database/storage failures.
- ESTIMATE: unified versioned content editing and operational reconciliation by driver; scope requires separate confirmation.

## Persistence

Physical bytes are not a SQL transaction. Storage objects and revisions have identity/provenance; domain bindings remain with their owners. Safe cleanup considers references, running leases and authorized roots, not merely cached reference counts.

## Recorded capabilities

- **FEAT-102** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] New/uploaded text assets, authorized folder browsing, ownership-aware cleanup, and platform-specific open actions.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-042 — Secret reference and purpose-scoped use:** caller.
- **CON-043 — Managed storage lifecycle:** declaration owner, implementation/integration boundary.
- **CON-052 — Authorized file browsing/opening:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-071 — Authorized content streaming for automation:** declaration owner, implementation/integration boundary.

## Proof and unresolved questions

Relevant planned scenarios: QA-014, QA-015, QA-016, QA-027, QA-036, QA-041, QA-043, QA-074, QA-075, QA-079, QA-080, QA-081, QA-082, QA-093, QA-094, QA-111, QA-113, QA-117, QA-140, QA-152, QA-153.

Related gaps: GAP-006, GAP-007, GAP-027.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
