# process-template-release-readiness-ui-live-host-closure-v1

## Status
Prepared for Codex implementation.

## Purpose
Close the remaining release-readiness gaps after `process-template-ui-live-e2e-runtime-readiness-v1`.

The previous bundle finally proved important representative process execution paths through process-mock launch plans, outbox dispatch, finalizer completion, artifact readback, and project/project-structure UI launch. However, final closure was explicitly blocked by the code-first ratio gate, live OpenAI template proof was not run in the last pass, runtime-host diagnostics are not yet visible in the operator UI, and the business-analysis PostgreSQL automation claim must be reconciled against the real code.

This bundle intentionally stays code-first and product-runtime focused. It must not create another large proof-only tree.

## Current release posture
The branch is close to restored process execution, but not merge-ready until:
- the final code-first ratio blocker is resolved honestly,
- the business PostgreSQL automation claim matches real code,
- runtime-host readback is operator-visible or explicitly deferred with a ticket and API proof,
- a bounded live OpenAI template smoke is either run successfully or explicitly skipped without claiming live proof,
- scheduler/workflow-origin process starts are proven through process-owned paths,
- representative process templates pass the final release matrix.

## Hard constraints
- Process Core must stay generic and dependency-clean.
- Do not introduce execution-capable process drivers.
- Do not introduce reflection discovery, fallback selectors, or driver self-registration.
- Do not mutate process state through driver/runtime-host surfaces.
- Do not count docs as implementation for code-first ratio.
- Do not create dozens of new proof files. Use concise transcripts and the execution report.
- Do not use `SuppressAutomationDispatch = true` as representative E2E proof.
- No small/medium/mobile UI optimization proof; large desktop only.

## Final closure requirement
Final closure is allowed only if all of these are true:
- `src + tests changed lines >= 5 × codex/bundles changed lines`, using an explicit start SHA captured in SB01.
- Build passes with 0 warnings and 0 errors.
- Full unit tests pass.
- Focused integration matrix passes.
- Large desktop Playwright project/process launch proof passes.
- Runtime-host readback UI/API proof is classified honestly.
- Live OpenAI template smoke is either passed with explicit env settings or explicitly skipped and not counted as live proof.
- Source scans pass for Core dependency drift, driver runtime side effects, reflection/fallback discovery, secret leakage, bundle path coupling, and large-file growth.
