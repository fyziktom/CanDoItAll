# Cognitive Memory P1 Beta Hardening

This follow-up bundle coordinates execution of the Cognitive Memory P1 roadmap phase from `docs/cognitive-memory/roadmap/roadmap.md`.

## Profile

- `initiative`

## Mission

- Move Cognitive Memory from P0-complete validation-grade alpha toward beta by stabilizing the HTTP API contract, adding provider-failure proof/runbooks, making retention cleanup explicit, exposing operator audit signals, hardening external source ingestion, adding performance guidance, and updating docs from source truth.

## Outcome Contract

- Requested outcome: continue with P1 as another follow-up bundle and use the CanDoItAll bundle workflow.
- Hard constraints: preserve existing public behavior, keep the legacy `/api/cognitive-memory` surface compatible, use strongly typed contracts, avoid silent fallback behavior, and update docs/roadmap based on executed changes only.
- Evidence required before closure: prepared-stage bundle validator, targeted build/tests, UI/browser proof for rendered operator changes, docs/roadmap update, completed-stage bundle validator, and explicit raw-note closure.
- Known blockers or explicit scope exceptions: live Qdrant/provider validation may be environment-gated if the local machine has no configured provider; in that case P1 must still add deterministic failure-path tests and an executable runbook.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `inventories/` source inventory and implementation touchpoints
- `plan/` execution order and dependency gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review, execution report, and browser proof

## Recommended Execution Order

1. `subbundles/01-api-contract-versioning`
2. `subbundles/02-provider-failure-runbooks`
3. `subbundles/03-retention-cleanup-policy`
4. `subbundles/04-operator-audit-surface`
5. `subbundles/05-external-source-hardening-and-performance`
6. `subbundles/06-docs-validation-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`

## Closure Summary

- P1 is complete for the local beta-hardening scope.
- Implemented v1 API aliases and contract metadata while preserving legacy route compatibility.
- Added deterministic projection provider-failure proof and a live-provider runbook.
- Added explicit dry-run-first retention cleanup for operational records with durable run audit.
- Added typed operator audit DTO/query/rendering on the health tab.
- Hardened external source ingestion limits, sensitive-content rejection, and extraction errors.
- Updated docs, roadmap, diagrams, runbooks, validation evidence, and bundle report.
- Remaining beta blocker: live Qdrant/provider validation in a configured environment plus broader production workflow browser proof.
