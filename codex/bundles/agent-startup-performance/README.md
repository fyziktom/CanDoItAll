# Agent Startup Performance

Prepare safer startup optimizations without changing the working agent execution or durability contract.

## Profile

- `feedback`; three implementation work units, each with Governed proof because filesystem security, provider integrity or recovery is involved.
- Prepared against `8a8dc2da0` on 2026-08-31. Preparation creates documentation only.

## Outcome Contract

- Improve actual preparation latency on native **5032** and Docker **5214** through the first three recommendations only.
- Preserve genuine agent conversations, context/skills, tools, approvals, errors, persisted history, source validation and crash recovery.
- **Implementation is not authorized by this bundle-preparation request. Do not execute the implementation prompt, tests, browser scenarios, deployments or live mutations until the user asks to execute.**
- Log accumulation/batching (recommendation 4), fire-and-forget progress, removing per-stage commits, weaker flushes and a logging redesign are **excluded**. The user's rationale about accumulating startup information is preserved in the raw input as a future direction, not silently added to this scope.
- No new projects, schema/journal migrations, public contract changes, default/provider edits, sibling changes or broad cleanup are planned.
- Readiness means the plan can be executed after authorization; it does not mean any optimization or regression test has run.

## Work Units

| Unit | Outcome | Prerequisite |
|---|---|---|
| [SB01](subbundles/01-operation-local-filesystem-facts/README.md) | Remove redundant case probes within verified filesystem-operation intervals | Phase 0 baseline and characterization |
| [SB02](subbundles/02-validated-provider-revision-projections/README.md) | Query/validate revisions without full runtime-profile materialization | Phase 0 baseline; independent of SB01 source |
| [SB03](subbundles/03-validated-immediate-commit-reuse/README.md) | Reuse validated immediate-commit plans under the same lock; retain complete recovery checks | SB01 gate; integrated closure also requires SB02 |

[Phase plan](plan/01-phase-plan.md) owns ordering and invalidation. [Test selection](plan/test-selection.md), [live UI matrix](plan/live-ui-validation.md), [performance protocol](plan/performance-validation.md) and [host safety](plan/host-safety.md) define the completion bar.

## Architecture And Evidence

[Current state](analysis/01-current-state.md), [boundary map](architecture/01-csharp-boundary-map.md), [architecture checkpoints](plan/architecture-checkpoints.md), [requirements](requirements/01-normalized-requirements.md), [traceability](traceability/01-requirement-traceability.md), [execution report](reviews/01-execution-report.md).

Source references use `repo://`; future proof uses `bundle://`. The repository convention `codex/bundles` is preserved. Transient local artifacts are not acceptance evidence until a sanitized, portable proof copy is recorded.

## UI Target Policy

- Real Playwright MCP interaction with **both** named instances, at **1920×1080 desktop**.
- Existing floating conversations, agent chat, progress, tool details and history are the proof surfaces. No UI redesign or mobile/BaseLib work.
- API reads may corroborate UI evidence; API-only runs, mocked completions or screenshots alone cannot pass.
- Existing close/Stop behavior is not assumed to cancel an active run.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Pass`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not started`

Preparation validation and independent review are recorded in [self-review](reviews/00-bundle-self-review.md). No implementation completion is claimed.
