# BND-COMPOSITION — Host, composition and control plane

**Target owner:** Host, composition and control plane

Implementation registration, profile lifetimes, migration/bootstrap, workers and shutdown, desktop/headless capabilities and HTTP adapters.

**Repository discovery location:** `None`

**Primary sources:** SRC-002, SRC-008, MAP-10

## Provided responsibilities

- Explicit module/adapter registration, runtime host-capability descriptors and short-operation scope factories.
- Control plane for profile activation/draining, complete schema composition, transfer coordination and worker lifetime.

## Required capabilities

- Concrete implementations and owner registration/lifecycle participants; composition does not transfer ownership.
- Host OS/database/storage configuration through responsible infrastructure and operator authorization.

## Forbidden shortcuts

- Composition may know every implementation for assembly, but must not absorb business logic or provide a global database backdoor.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: distinguish lightweight presentation hosts from runtime feature hosts; fence profile lifetime and preserve bootstrap/upgrades.
- ESTIMATE: more multi-instance/runtime partitioning only as needed, not immediate microservices or a generic plugin framework.

## Persistence

A complete design-time schema model can know all mappings; applications must not receive it as a global writer. Control-plane operation plans have their own persistence; domain data moves through participant owner APIs.

## Recorded capabilities

- **FEAT-107** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Runtime host profiles, control-plane database sources, hosted workers, bootstrap, and web adapters.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-012 — AI resource projection/binding reconcile:** caller.
- **CON-045 — Post-commit invalidation/live progress:** declaration owner, implementation/integration boundary.
- **CON-047 — Workspace preference API:** caller.
- **CON-048 — Database profile and host capability control:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** declaration owner, caller.

## Proof and unresolved questions

Relevant planned scenarios: QA-007, QA-019, QA-028, QA-041, QA-042, QA-045, QA-077, QA-084, QA-085, QA-087, QA-089, QA-090, QA-091, QA-092, QA-095, QA-096, QA-097, QA-100, QA-103, QA-106, QA-111, QA-114, QA-121, QA-165.

Related gaps: GAP-016, GAP-019, GAP-024, GAP-027, GAP-030, GAP-032, GAP-041.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
