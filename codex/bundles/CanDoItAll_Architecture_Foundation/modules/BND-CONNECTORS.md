# BND-CONNECTORS — Connectors / external integrations

**Target owner:** Connectors / external integrations

Manifest/schema/driver registries and concrete command adapters, with durable delivery where needed.

**Repository discovery location:** `None`

**Primary sources:** MAP-11, SRC-019

## Provided responsibilities

- Manifest/capability/configuration-schema mechanisms and transport-adapter registries.
- Explicit typed command/result boundaries only for actually registered, supported handlers.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Concrete provider/resource/plugin configuration references and authorization; purpose-bound Security resolution.
- Host/runtime I/O adapters and the affected owner service for business mutations.

## Forbidden shortcuts

- A schema renderer does not own configuration. A dormant outbox is not a completed integration. Plugin and connector are not synonyms.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: distinguish configuration rendering/manifests from executable capabilities.
- UNPROVEN: complete current handler coverage; absence in an old map proves no absence now. Unsupported never means fake success.

## Persistence

Registries and durable command records have their own operational lifecycle. Manifest metadata is not a second provider/resource master. Configuration changes use owner APIs; arbitrary settings JSON is not an escape hatch.

## Recorded capabilities

- **FEAT-106** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Connector manifest sources and schema renderers integrate Resources, Providers, and Workspace.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-041 — Connector execution:** declaration owner, implementation/integration boundary.

## Proof and unresolved questions

Relevant planned scenarios: QA-103, QA-105, QA-112.

Related gaps: GAP-015, GAP-031.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
