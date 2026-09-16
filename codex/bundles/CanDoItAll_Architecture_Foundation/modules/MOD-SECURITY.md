# MOD-SECURITY — Security: secrets, identities and access enforcement

**Target owner:** Security; domains own resource policy, transports validate incoming identity

Secure secret storage and purpose-limited release, access/capability policy mechanics and references. HTTP authentication and domain authorization must not be confused with text requested by an agent.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Security`

**Primary sources:** SRC-017, SRC-003, SRC-009, MAP-11

## Provided responsibilities

- Secret-reference queries without values and purpose-scoped resolution/use handles.
- Policy and audit identity for a host-verified caller context.
- Secret lifecycle commands and reference-aware deletion checks.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Host platform capabilities and secure key storage.
- Reference participants from Providers, Storage and Plugins to validate usage.

## Forbidden shortcuts

- Do not confuse a profile ID with a permission or tenant.
- Do not store plaintext in logs, exceptions, DTOs, projections or exports.
- Do not introduce permissive default policies to simplify sandbox DI.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: explicit execution authority and revocation at the point of use.
- ESTIMATE: advanced enterprise identity/residency policies; do not expand scope without an assignment.

## Persistence

Vaults and key material may live outside the business database; do not export them implicitly with a project. Profile switching or database restore must not reuse secrets from the previous scope.

## Recorded capabilities

- **FEAT-080** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Security runtime and UI.
- **FEAT-081** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] HTTP bearer authority is not an agent capability grant.
- **FEAT-082** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Provider preparation excludes secret values; secrets are resolved at dispatch.
- **FEAT-083** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Secret metadata, vault service, resolver, protectors, startup migration, and wrapping key.
- **FEAT-084** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Plugin secret broker, storage resolver, and deletion guards.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-042 — Secret reference and purpose-scoped use:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.

## Proof and unresolved questions

Relevant planned scenarios: QA-009, QA-010, QA-012, QA-018, QA-024, QA-033, QA-054, QA-058, QA-068, QA-069, QA-096, QA-102, QA-111, QA-116.

Related gaps: GAP-021, GAP-024.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
