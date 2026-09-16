# MOD-PROVIDERS — Agents / Providers: administration, pricing, sharing and usage

**Target owner:** Agents / Providers

Authority over provider profiles, publications, import bindings, model offers and tariffs. Transport protocols belong to adapters; provider execution belongs to the respective runtime. Historical invocation valuation is immutable evidence, not a live tariff copy.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement`

**Primary sources:** SRC-022, SRC-007, MAP-11

## Provided responsibilities

- Provider administration and query contracts; model, capability and health catalogs without secrets.
- Cost quotes and versioned tariff summaries for Agents, CRM and Work Management.
- Publication, import and synchronization using provider-neutral contracts; immutable usage evidence.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Security for secret lifecycle.
- Storage, transport and network policy for external communication.
- Execution runtimes for health checks, probes, invocations and evidence.
- Workspace preferences reference opaque provider IDs rather than editable copies.

## Forbidden shortcuts

- CRM must not maintain a second AI price list.
- Workspace must not own a profile merely because its table is named Workspace_ProviderProfiles.
- Caches, logs, quotes and UI must not expose resolved secrets.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: one price-calculation owner and explicit unknown, free, estimated and actual states.
- ESTIMATE: planning quotes for composite agents or workflows with uncertainty and limits.
- ESTIMATE: scope-specific quotas and budgets; do not introduce a speculative billing aggregate.

## Persistence

Preserve identities, imported publication/source bindings, tariff snapshots and request history. Moving the writer does not require cosmetic table renames. Distinguish relay and local usage to avoid charging for the same invocation twice.

## Recorded capabilities

- **FEAT-010** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Provider administration, publishing, source/import management, catalog routing, and audit history.
- **FEAT-011** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Model transport, purpose, driver, and registration do not establish endpoint health.
- **FEAT-012** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Input/output, cache, and long-context prices distinguish missing pricing from a zero price.
- **FEAT-013** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Shared provider subsets retain provenance and routing information.
- **FEAT-014** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Invocations freeze tariff information and application-observed attempts; not every SDK retry is independently observed.
- **FEAT-015** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] ProviderConnectorManifestSource, secret deletion guards, and database transfer integration.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-005 — AI workload cost quote:** declaration owner, implementation/integration boundary.
- **CON-006 — Provider administration:** declaration owner, implementation/integration boundary.
- **CON-007 — Provider execution profile lease:** declaration owner, implementation/integration boundary.
- **CON-008 — Shared-provider publish/import/sync:** declaration owner, implementation/integration boundary.
- **CON-009 — Usage evidence and allocation query:** declaration owner, implementation/integration boundary.
- **CON-042 — Secret reference and purpose-scoped use:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.

## Proof and unresolved questions

Relevant planned scenarios: QA-043, QA-049, QA-050, QA-051, QA-052, QA-053, QA-054, QA-055, QA-097, QA-101, QA-102, QA-111, QA-121, QA-149.

Related gaps: GAP-001, GAP-008, GAP-012, GAP-018, GAP-024, GAP-025, GAP-028.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
