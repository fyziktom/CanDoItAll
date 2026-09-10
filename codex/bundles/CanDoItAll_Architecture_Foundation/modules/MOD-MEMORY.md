# MOD-MEMORY — Memory: providers, derived knowledge and provenance

**Target owner:** Memory

Memory configuration, operations, ingestion/retrieval and source provenance. Memory is derived context, not alternative CRM/project truth. Source owners determine exportable snapshots and permissions.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Memory`

**Primary sources:** SRC-018, SRC-003, MAP-11, MAP-07

## Provided responsibilities

- Memory operation/query/status and provider-configuration application contracts.
- Ingestion contracts with source identity, revision and access metadata.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Source-owner snapshots and Security authorization.
- Provider, transport, embedding and inference adapters according to configuration.

## Forbidden shortcuts

- Memory must not authoritatively repair agents, parties or tasks from retrieval text.
- Do not embed plaintext secrets or add them to generic context.
- Do not couple source domains to a specific vector provider.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: source scope/revision and revocation across all indexes and caches.
- ESTIMATE: improved relevance, evaluation and retention lifecycle; outside a behavior-preserving refactor.

## Persistence

Memory owns derived indexes and operation state. Rebuilding or deleting an index must not delete its sources. Revocation and source tombstones must block access even while physical cleanup is pending.

## Recorded capabilities

- **FEAT-070** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Memory providers, diagnostics, operations, navigation, services, and contracts.
- **FEAT-071** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Memory tools expose provider-backed context queries and operation status.
- **FEAT-072** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Neutral Memory operation model with HTTP, MCP, mock-service, and driver integration.
- **FEAT-073** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Memory source gateways for CRM, Resources, and Workbench ingress.
- **FEAT-074** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Memory provider configuration, UI, controller, and registry dependencies require API/UI boundary verification.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-038 — Memory operation API:** declaration owner, implementation/integration boundary.
- **CON-039 — Memory source snapshot publication:** declaration owner, caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-075 — Memory ingestion of owner-published evidence:** declaration owner, implementation/integration boundary.

## Proof and unresolved questions

Relevant planned scenarios: QA-065, QA-086, QA-088, QA-116, QA-157.

Related gaps: GAP-020.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
