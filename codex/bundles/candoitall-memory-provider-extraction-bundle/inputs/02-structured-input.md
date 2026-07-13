# Structured Input

## Initiative type

- Large multi-phase architecture migration.
- Cross-project separation and optional native provider integration.
- Requires new contracts, generic provider runtime, source gateway, MAF integration, UI composition, native service extraction, persistence separation, migration cleanup, and regression proof.

## Non-negotiable outcomes

- CanDoItAll base startup does not require Qdrant or native Cognitive Memory.
- Generic memory provider module supports zero, one, or many providers.
- Agents, workflows, and processes select memory providers through configuration and policy.
- MAF depends only on generic memory contracts, not native Cognitive Memory classes.
- Native Cognitive Memory owns its engine, DB, migrations, workers, advanced UI, and optional Qdrant projection.
- Native memory can be integrated through the same protocol as other providers.
- Source ingestion is snapshot-based and policy-governed.
- Long-running memory operations and eventful memory behavior are first-class.
- Feedback correlation, delayed feedback, retention, and optional IPFS snapshots are supported.
- Checkpoint subbundles must refactor and harden foundations before dependent phases start.

## Planning posture

Use a strangler approach: first build generic contracts and runtime in the main app, then adapt the current in-process Cognitive Memory through a generic provider boundary, then move native engine code into the separate service and switch the main host to a remote/native provider driver, then remove old direct references and Qdrant/base startup coupling.

## Explicit scope exclusions

- Do not implement code in this bundle package.
- Do not require final economic-governance memory features to be complete before extraction.
- Do not force every future memory provider to implement advanced native features.
- Do not replace all source modules; source gateway adapters should start with high-value modules and remain extensible.
