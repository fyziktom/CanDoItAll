# Codex execution plan

## Guiding principle

Treat this as a controlled hardening of the canonical process module, not as a loose cleanup pass.

## Batch order

### Batch A — baseline and canonical foundation
1. Baseline characterization and live-gap reconciliation.
2. Canonical dependency model and compatibility boundary.
3. Side-effect-free validation and editor-normalization split.
4. Architecture review gate A.

### Batch B — atomicity and persistence stability
1. Transaction, concurrency, and conflict hardening.
2. Differential definition-graph persistence.
3. Architecture review gate B.

### Batch C — publication, runtime, and read side
1. Publication, versioning, and clone-engine decomposition.
2. Runtime state-machine and transition-policy extraction.
3. Read-side query splitting and performance hardening.
4. Architecture review gate C.

### Batch D — consolidation and decomposition
1. Template subsystem and cross-module shared-infrastructure consolidation.
2. Workspace and canvas decomposition.
3. Schema hygiene, migrations, and long-file split.
4. Architecture review gate D.

### Batch E — closure
1. Final regression proof and bundle closure.

## Required review cadence

After every few implementation subbundles, Codex must stop and answer the explicit architecture questions in the corresponding review gate. If the answer is negative, incomplete, or uncertain, Codex must open corrective work first.

## Completion rule

Do not call the run complete unless:
- all gate memos exist,
- any corrective subbundles are closed,
- all required proof is recorded,
- the completed-stage validator passes.
