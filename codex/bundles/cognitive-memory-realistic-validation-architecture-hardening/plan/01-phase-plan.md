# Phase Plan

## Phase 1: Validation Host And Static Assets

- Reproduce the no-build/static asset failure.
- Define the supported local startup modes for validation runs.
- Add startup diagnostics for missing static web assets.
- Validate Development and production-like startup paths explicitly.

## Phase 2: Clean Environment Orchestration

- Add API/UI status fields for active profile source, override reason, database name, and projection provider readiness.
- Add an idempotent clean validation profile creation flow with clear operator confirmation.
- Add Qdrant collection readiness checks and collection metadata inspection.

## Phase 3: Source Truth Transfer Completeness

- Extend transfer preview and execution to include external file/data manifests.
- Add content-hash proof, redaction-state proof, and skipped-secret proof.
- Preserve project/project-structure identity and source locators.

## Phase 4: Policy-Preserving Operations

- Store full policy context on probe sessions.
- Use the stored policy when asking probe turns.
- Carry policy context into quality planning, dreaming, recall, and review audit rows.
- Add tests that restricted validation sessions can recall restricted source truth when explicitly allowed.

## Phase 5: Dream Aggregate Quality

- Improve aggregate title/body generation from primary cluster keys and source snippets.
- Add quality gates that flag structural-only aggregates before human approval.
- Add dream aggregate review-decision audit and application semantics.

## Phase 6: Probe And Recall Loop

- Pass projection collection/profile/embedding options through probe ask requests.
- Add regression tests for probe feedback that creates repair candidates.
- Add UI/API proof that a probe correction can be reviewed and consolidated.

## Phase 7: Qdrant Projection Operability

- Add default projection profile diagnostics.
- Add projection rebuild status summaries per project and collection.
- Add recall traces that distinguish configured vector search, skipped vector search, and provider failure.

## Phase 8: Long-Run Validation Orchestration

- Add validation cycle IDs, resumable cursors, and per-cycle metrics.
- Run repeated consolidation/dreaming/probe/recall cycles under controlled approval gates.
- Produce an XLSX ledger with counts, operation IDs, accepted/rejected memories, recall traces, and unresolved findings.
