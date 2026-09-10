# MOD-RESOURCES — Resources: reusable material catalog

**Target owner:** Resources

Catalog metadata and connector/content references. A Resource is neither a CRM staffing resource nor every storage object. File access uses an authorized FileTools scope.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Resources`

**Primary sources:** SRC-014, MAP-11

## Provided responsibilities

- Resource catalog queries/commands, storage binding descriptors and source snapshots.
- Structure projection contributions, resource attachment references and authorized file-browse scopes.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Projects existence/scope; Storage/FileTools content; Connector manifests/settings.
- Memory ingestion and Agents contextual launch through narrow ports only.

## Forbidden shortcuts

- Do not read Projects EF entities for catalog queries or move generic connector UI into a Workspace dependency.
- Do not own connector secrets or use a physical path as business identity.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: a Projects lookup contract and correct content references/retention.
- ESTIMATE: versioned resources and improved connector health/reference validation; do not call every legacy type a complete connector.

## Persistence

Resources_ProjectResources belongs to Resources. A storage object can be shared by multiple Resource and Structure bindings; physical deletion is not simply Remove(Resource).

## Recorded capabilities

- **FEAT-059** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Resource records and reusable workspace materials.
- **FEAT-060** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] ProjectResource kinds and CRUD operations.
- **FEAT-061** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Connector kinds include Webhook, StorageObject, and legacy forms.
- **FEAT-062** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] File catalog, bindings, browsing, and promotion.
- **FEAT-063** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Resources page and floating-agent context.
- **FEAT-064** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Resource snapshots are provided to Memory.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-015 — Project lookup:** caller.
- **CON-021 — Ensure structure contribution:** caller.
- **CON-022 — Asset binding and content lifecycle:** caller.
- **CON-031 — Process run result publication:** caller.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-035 — Resource catalog and promotion:** declaration owner, implementation/integration boundary.
- **CON-038 — Memory operation API:** caller.
- **CON-039 — Memory source snapshot publication:** implementation/integration boundary, extension adapter.
- **CON-041 — Connector execution:** caller.
- **CON-043 — Managed storage lifecycle:** caller.
- **CON-044 — Context surface capture:** implementation/integration boundary, extension adapter.
- **CON-046 — Project lifecycle participant protocol:** implementation/integration boundary, extension adapter.
- **CON-052 — Authorized file browsing/opening:** caller.
- **CON-054 — Agent run result publication:** caller.
- **CON-055 — Workflow run result publication:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-069 — Authorized Resources discovery and retrieval metadata:** declaration owner, implementation/integration boundary.
- **CON-070 — Resource catalog creation and metadata administration:** declaration owner, implementation/integration boundary.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.

## Proof and unresolved questions

Relevant planned scenarios: QA-014, QA-065, QA-075, QA-076, QA-080, QA-083, QA-099, QA-108, QA-112, QA-119, QA-140, QA-152, QA-153, QA-154, QA-160.

Related gaps: GAP-006, GAP-015, GAP-038, GAP-039.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
