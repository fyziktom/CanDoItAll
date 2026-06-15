# User Story Coverage Validation

## Purpose

This validation plan defines how future agents prove that the rewritten Process module preserves or intentionally improves the current user-facing Process capabilities identified in `analysis/06-current-implementation-user-story-map.md`.

## Validation Levels

| Validation level | Applies to | Required evidence |
| --- | --- | --- |
| Source proof | Every story | New source files implementing the model, projection, command, strategy, adapter, or UI surface. |
| Unit proof | Core, builder, branch, artifact, policy, strategy, manager rules | Focused tests for invariants, negative cases, idempotency, limits, and typed contract validation. |
| Integration proof | Runtime, persistence, outbox, artifact ledger, projections, template migration, Git wrapper | Tests using real storage or representative integration harnesses with crash/retry/replay cases where relevant. |
| Component proof | Blazor components and shared Git components | Component rendering and user action tests against projection DTOs and command test doubles. |
| Playwright proof | Browser-facing stories | Route, viewport, actions, assertions, screenshot, accessibility snapshot when useful, and console/network issue review. |
| Scan proof | Boundaries and security | Dependency scans, domain vocabulary leak scans, old-symbol scans, redaction scans, UI runtime-internal scans. |

## Browser-Facing Story Proof

Browser-facing stories include US-001 through US-049 and US-056. A future subbundle that owns any of these stories must record:

- Route under test.
- Project/global scope used.
- Viewport dimensions.
- User actions performed.
- Assertions made against visible state.
- Screenshot path under that subbundle's proof directory.
- Accessibility snapshot or equivalent DOM evidence when the state is complex.
- Console and network issue summary.
- Projection freshness/lag checks when the story reads live/history data.

Browser proof cannot be postponed to SB28 unless the story is only a final cross-flow regression. The owning UI subbundle must produce first proof.

## Non-Browser Story Proof

Stories US-050 through US-056 require API/tool/security/readiness proof even when they also have UI effects:

| Story | Required proof |
| --- | --- |
| US-050 | Process agent tool facade tests for save, publish, delete, export, import, template query, and run operations. |
| US-051 | Template pack index tests for baseline scenarios and live run profiles after migration. |
| US-052 | Parent/child manager message tests and subprocess artifact propagation integration tests. |
| US-053 | Missing artifact recovery/resupply strategy tests and artifact ledger provenance tests. |
| US-054 | Dispatcher claim, outbox retry, stale lease, dead-letter, projection offset, and recovery tests. |
| US-055 | Policy, access summary, redaction, and Git unauthorized mutation audit tests. |
| US-056 | Role execution requirement compiler tests, candidate suitability scoring tests, deterministic readiness evaluator tests, missing tool/right blocker tests, provisioning reassessment tests, redaction tests, and launch UI projection proof. |

## Required Negative Tests

Future implementation must include negative tests for:

- UI component attempts to reference runtime internals or EF runtime entities.
- Generic core/runtime attempts to reference domain-specific vocabulary from current templates/drivers.
- Branch route uses free-text token routing instead of typed route targets.
- Backward branch route exceeds loop budget and fails to escalate.
- Missing artifact recovery loops without fingerprint/budget enforcement.
- Live 1h query returns historical events older than the requested window.
- Template migration skips an intermediate migration and still claims success.
- Markdown or Mermaid projection is treated as canonical source.
- Manager exposes raw sensitive diagnostics directly to the UI.
- Agent modifies unauthorized files and the Git audit does not detect it.
- High-scoring HR candidate with missing required tool or missing required right is marked executable.
- Missing tool/right readiness is hidden only in recommendation summary text instead of typed findings.
- Provisioning task completion clears a readiness blocker without a fresh reassessment.

## Story Coverage Report Format

Every future subbundle must add or update its execution report with:

| Story ID | Coverage decision | Source proof | Test proof | Browser proof | Notes |
| --- | --- | --- | --- | --- | --- |

Allowed coverage decisions:

- `Implemented`.
- `Improved equivalent`.
- `Deferred to named downstream subbundle`.
- `Removed with explicit user approval`.

`Deferred` is only valid when the downstream subbundle is listed in `traceability/04-user-story-coverage-map.md`. SB28 cannot close while any story remains deferred.

## Final Closure Requirements

SB28 must prove:

- All US-001 through US-056 stories have final coverage decisions.
- All browser-facing stories have screenshot proof from their owning subbundle or SB28 regression proof.
- All non-browser stories have source and automated test proof.
- All replacement/improvement decisions are documented and traceable.
- All approved removals are explicitly recorded.
- No story is covered by active old runtime/dispatcher fallback.
- No story relies on query-side recomputation of runtime truth.
- Live/history time-window behavior is correct for `Live 1h`, `1 day`, `7 days`, and `30 days`.
- Final E2E source scenarios are replayed through public APIs and scenario-specific vocabulary does not leak into generic Process code.
