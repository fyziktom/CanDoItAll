# MOD-WORKSPACE — Workspace: environment settings and presentation

**Target owner:** Workspace

Workspace preferences and working-environment identity; settings of other domains are composed only. Database-profile administration is control-plane work; secrets belong to Security, providers to Agents, and the storage catalog to Storage.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Workspace`

**Primary sources:** SRC-021, MAP-11

## Provided responsibilities

- Scoped preference queries/updates and active-workspace information.
- Settings composition through owner contracts; a UI registry may be extracted into a neutral presentation boundary.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Providers catalog, Storage administration, Security/API access and control-plane database profiles.
- Connector manifest/command lifecycle for actually activated handlers.

## Forbidden shortcuts

- Do not store arbitrary cross-module entities as Workspace settings.
- No new provider truth in Workspace.
- Do not retarget an in-flight operation to the database currently selected in the UI.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: distinguish host/control-plane identity, domain workspace and UI editor instance.
- REQUIRES VERIFICATION: dormant connector outbox and actual handlers; do not delete them based only on old analysis.
- ESTIMATE: administrative capability catalogs and improved standalone hosts, without permissive fallbacks.

## Persistence

Workspace_Settings contains owned preferences. Other historically colocated tables receive owners according to meaning, not prefix. The platform coordinates profile transfer and migration.

## Recorded capabilities

- **FEAT-088** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Workspace records, state, and currently centralized cross-module services.
- **FEAT-089** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] DefaultProviderProfileId is an opaque Workspace preference.
- **FEAT-090** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Settings cover database/schema health, transfer, storage, provider history, file applications, vault, and API tokens.
- **FEAT-091** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Connector registry, schema UI, and storage components.
- **FEAT-092** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Connector outbox infrastructure exists; actual production handlers remain unverified.
- **FEAT-093** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Provider-consumer ports and transfer handlers.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-001 — Agent catalog / batch lookup:** caller.
- **CON-006 — Provider administration:** caller.
- **CON-016 — Project lifecycle orchestration:** caller.
- **CON-040 — Plugin lifecycle/capability API:** caller.
- **CON-041 — Connector execution:** caller.
- **CON-042 — Secret reference and purpose-scoped use:** caller.
- **CON-047 — Workspace preference API:** declaration owner, implementation/integration boundary.
- **CON-048 — Database profile and host capability control:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.

## Proof and unresolved questions

Relevant planned scenarios: QA-007, QA-096, QA-111, QA-112.

Related gaps: GAP-015, GAP-024.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
