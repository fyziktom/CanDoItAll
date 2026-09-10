# BND-MAF — Neutral runtime / SDK boundary

**Target owner:** Neutral runtime / SDK boundary

Provider/LLM/tool/executor abstractions and generic execution algorithms; external SDK isolation.

**Repository discovery location:** `None`

**Primary sources:** SRC-003, SRC-027, MAP-07

## Provided responsibilities

- Neutral provider/LLM/tool/executor contracts, descriptor composition and generic execution mechanics.
- SDK adapters with controlled failure/cancellation semantics and no product-specific master models.

## Required capabilities

- Registered capability/executor adapters and verified purpose/scope; explicit provider-neutral configuration.
- External SDKs inside adapters; product tool-policy inputs through published contracts.

## Forbidden shortcuts

- Concrete CRM, Project Structure, Prompt Gallery and Scheduler semantics belong to product-owned packs/adapters, not generic Core.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: move product policy/vocabulary to owner packs without deleting needed tools.
- OUT OF SCOPE: SDK/MAF upgrades and concurrent tools; preserve serial order and approval barriers.

## Persistence

Not a universal application store. Runtime checkpoints, usage and operation persistence need explicit execution owners. Preserve serialized payload/version compatibility when moving product types out of neutral Core.

## Recorded capabilities

- **FEAT-108** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] RuntimeToolProviderComposer evaluates descriptors, purposes, and effective invocation context.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.


## Proof and unresolved questions

Relevant planned scenarios: QA-040, QA-114.

Related gaps: GAP-017, GAP-031.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
