# Agent Startup Performance

Implement the three approved startup optimizations while preserving the working agent execution and durability contract.

## Profile

- `feedback`; three implementation work units, each with Governed proof because filesystem security, provider integrity or recovery is involved.
- Prepared against `8a8dc2da0` on 2026-08-31. Preparation creates documentation only.

## Outcome Contract

- Improve actual preparation latency on native **5032** and Docker **5214** through the first three recommendations only.
- Preserve genuine agent conversations, context/skills, tools, approvals, errors, persisted history, source validation and crash recovery.
- The original request authorized preparation only. The subsequent request in inputs/04-execution-request.md authorizes implementation and proper testing, including the planned controlled replacements of 5032 and 5214.
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
- Execution status: `Completed` — all three approved optimizations, focused gates, real two-host UI/tools/approvals, paired performance and final host preservation passed; retained broad-test exceptions remain explicit. Authorized by inputs/04-execution-request.md and inputs/05-file-validation-approval.md.
- Subbundle gate review: `Pass` — all three implementation units, scoped architecture and integrated real UI/performance/host evidence accepted by root.
- Final closure gate: `Pass` — root and independent review accepted actual behavioral/host proof; unchanged canonical completed-stage validation passed.
- Browser validation analytics: `Pass` — both named hosts, required UI01–UI06 and applicable approval matrix; native busy-Stop proof retains its preparation/admission limit.

Preparation validation and independent review are recorded in [self-review](reviews/00-bundle-self-review.md). Measured performance passed. The user granted protected-file transmission permission in inputs/05-file-validation-approval.md; the five source-file runs and full history reload now have actual evidence. Native approval rejection and its reload proof also passed. Root accepted the final host checkpoint and behavioral closure.

Execution started from committed bundle/source HEAD 3d5def561. Earlier preparation-only statements describe the previous request; the new execution request authorizes the implementation and planned tests. No managed dotnetwatch app is active; the existing 5032 Release process was retained for the baseline.

## Frozen candidate checkpoint

All three focused gates, combined failures, architecture reviews and native/Docker builds passed. Candidates are healthy after controlled replacement; rollback evidence is preserved and publisher 5210 is unchanged.

The first broad run remains failed: 9,747 cases, 9,731 passed, 15 failed and one disabled opt-in skip; 39 deferred theory expansions reconcile discovery. History follow-up passed 23/23 after increasing only disposable PostgreSQL capacity. Preview retries passed 2/2 but their initial cause is unproven. Two pre-existing Unit guards and the strict planner index-name guard remain unresolved. No broad suite was repeated. Root accepted startup-specific progression with these retained findings, not an all-green broad gate.

Independent actual-UI performance passed: native median 12.152916 to 6.321676 seconds (47.982% reduction), client 31.669973 to 22.449116 seconds (29.115%), with no repeat trigger or submit regression. Fourteen runs were measured: seven runs in six conversations per host.

Real missing-path tools, safe continuation and bounded close/reload/error-history checks passed; file-browser styles and readable models were inspected. Authorized protected-file comparisons/follow-ups and successful-file history passed on both hosts. Native pending-handle reopening and approval acceptance resumed the same run once, with one conversion receipt. The separate native rejection case and reload proved a durable rejected decision without conversion execution. Root accepted the final host checkpoint and behavioral closure. Source-backed tests supplement, rather than substitute for, the earlier error cases. Execution is Completed; canonical final validation passed. See proof/closure-preparation/completed-validator.log and its exact command metadata.

See [UI checkpoint](proof/SB03/ui/validation-summary.md), [performance verification](proof/SB03/performance/independent-result-verification.json), [broad results](proof/frozen-integration/final-broad-and-attribution-summary.json) and [final host checkpoint](proof/deployment/final-checkpoint.json).
