# MAF Processes Merge Hardening Polish v1

This bundle is a merge-preparation hardening package for `maf-processes-merge-hardening-polish-v1` on branch `maf-processes-refactor` before merging into `development`.

## Profile

- `initiative`

## Mission

Prepare the process refactor branch for merge without a broad runtime rewrite. The target state is a clean branch with transient Codex work-package artifacts removed, no work-package/subbundle naming leaks in active tests, clearer domain-driver ownership for software-delivery proof rules, and reinforced boundaries around Process Core, domain drivers, the explicit verification gateway, and the remaining process dispatcher runtime.

## Outcome Contract

- Requested outcome: deliver a controlled polishing/hardening implementation plan that Codex can execute in subbundles before merging `maf-processes-refactor` into `development`.
- Hard constraints:
  - Do not start the planned dispatcher-runtime isolation after merge.
  - Do not introduce a dynamic process driver host, registry, selector, plugin discovery, runtime command surface, scheduler hook, workflow hook, or manager command.
  - Do not reconnect MAF to `CanDoItAll.Modules.Processes`.
  - Do not remove the working multi-team app delivery behavior that produced the Tetris game.
  - Treat `codex/bundles` and `codex/bundle-exports` as transient local helper surfaces, not production repo content.
  - Replace work-package numbering in tests with semantic names; do not preserve `SB###`, `INV###`, bundle slug, or subbundle terms in active test names.
- Evidence required before closure:
  - Source scans proving no tracked transient work-package artifacts remain.
  - Unit tests proving repository artifact hygiene, process/driver boundaries, and no MAF -> Processes compile-time reference.
  - Process/driver focused unit tests and process-filtered integration tests pass.
  - Solution build passes.
  - Existing live multi-team delivery smoke evidence is preserved or a fresh documented smoke run is executed when the environment is available.
- Known blockers or explicit scope exceptions:
  - Large dispatcher-runtime isolation is intentionally deferred until after merge.
  - Browser/UI validation is only required if the implementation touches UI surfaces. The expected work is mostly source/test/package hygiene.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report template
- `inventories/` source inventory and audit findings
- `templates/` reusable subbundle README template

## Recommended Execution Order

1. `subbundles/01-repository-artifact-hygiene-and-bundle-leak-cleanup`
2. `subbundles/02-test-naming-neutralization-and-guardrails`
3. `subbundles/03-software-delivery-domain-proof-driver-extraction`
4. `subbundles/04-driver-boundary-and-gateway-hardening`
5. `subbundles/05-merge-validation-and-live-process-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, `inventories/01-scope-inventory.md`, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `N/A unless UI is touched`
