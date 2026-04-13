# Normalized requirements

## BRQ-001 Initiative-grade bundle structure

The bundle must follow the in-repo initiative pattern and be ready for Codex execution, not just human reading.

## BRQ-002 Live-repository grounding

The bundle must be based on the current repository state, not on stale assumptions or only on the prior narrative.

## BRQ-003 Canonical dependency model

The process dependency model must have one canonical representation for authoring, persistence, clone, read, and runtime logic.

## BRQ-004 Explicit compatibility boundary

Legacy dependency fields may only survive behind a narrow compatibility boundary or migration bridge, not as a live second source of truth.

## BRQ-005 Pure validation

Validation must not mutate editor or aggregate state. Normalization must be explicit, intentional, and idempotent.

## BRQ-006 Atomic save, publish, and transition

Critical mutation flows must be transactionally safe and conflict-aware.

## BRQ-007 Provider-agnostic optimistic concurrency

Aggregate roots involved in definition and runtime mutation must use optimistic concurrency that works across the current providers.

## BRQ-008 Differential graph persistence

Definition save must update the child graph surgically so stable children preserve identity.

## BRQ-009 Publish/version hardening

Publish and clone responsibilities must be separated enough that version, slug, and draft-generation behavior is deterministic and race-aware.

## BRQ-010 Runtime state-machine extraction

Runtime transition rules must be extracted into smaller, testable policies and planners while preserving the public command surface.

## BRQ-011 Read-side query hardening

Common list, detail, and analytics queries must use slimmer projections and avoid unnecessary broad in-memory aggregation.

## BRQ-012 Cross-module duplication reduction

Duplicated helpers around slug generation, JSON file loading, enum parsing, and role snapshot summaries must be consolidated intentionally.

## BRQ-013 Workspace and canvas decomposition

The large workspace/canvas surfaces must be split into smaller components and/or explicit state holders without moving domain logic into the UI layer.

## BRQ-014 Schema and model hygiene

Large model/config files and relationship configuration must be made easier to audit, with coherent migration handling across both providers.

## BRQ-015 Regression and proof discipline

Execution must include prepared/completed validator passes, targeted .NET proof, browser proof where relevant, and updated execution-report artifacts.

## BRQ-016 Repeated architecture review gates

After every few subbundles, Codex must stop for an architecture review gate and may continue only if the gate passes.

## BRQ-017 Corrective-first continuation

If a gate fails, Codex must add and complete corrective subbundles before downstream work continues.

## BRQ-018 Detailed Codex instructions

Each subbundle must include exact source references, implementation steps, proof requirements, and stop rules.

## BRQ-019 Zip-deliverable output

The final preparation artifact must be deliverable as a zip file.

## BRQ-020 No new monoliths

The remediation must not replace the current monoliths with differently shaped monoliths.

## BRQ-021 Thin façade compatibility

Where useful, `ProcessesService` may remain the public façade, but responsibility-bearing internals must split.

## BRQ-022 Shared extraction discipline

Only genuinely shared helpers may move into neutral shared locations. Domain-specific logic must keep domain ownership.
