# Processes Hardening Follow-up: Runtime Integrity, Lineage, and Unblock Reliability

## Status

Prepared for Codex execution.

## Branch Context

The user wrote `process-hardening`, but GitHub branch search returned no exact branch with that name. The available branch is `processes-hardening`.

Reviewed branch:

- `repo://fyziktom/CanDoItAll@processes-hardening`
- Current reviewed head: `df62c356f9192d632a3a3a0f20244e641ec9e969`
- Commit message: `phase3`

## Mission

Perform the next hardening pass on CanDoItAll `Processes` after the phase3 implementation.

The previous phases improved the runtime substantially:

- process operation metadata exists
- tool policy uses process product-mutation metadata
- workflow and subprocess paths are routed through process-owned finalization
- upstream artifact materialization is journaled
- downstream reactivation exists
- process definition lint exists

The remaining problem is runtime integrity: several mechanisms still depend on fragile string matching, truncated external reference keys, unsaved EF state, heuristic artifact mapping, and partial tool-policy visibility. This bundle closes those gaps.

## Most Important Current Risks

1. Downstream reactivation after upstream artifact materialization can miss the just-created artifact because reactivation queries persisted artifacts before the new artifact is saved.
2. Manager recovery lineage is encoded into `ExternalReferenceKey`, but process artifact external reference keys are truncated to 200 characters.
3. Non-mutating steps can still invoke script/run tools that mutate product files indirectly because tool policy sees only tool arguments, not helper script contents or side effects.
4. External-target grounding is still derived from free text in trigger reasons, work briefs, upstream artifact summaries, and provenance, which can accidentally ground stale or sibling paths.
5. Artifact validation still does not reliably read storage-backed content for relative managed storage paths.
6. Workflow and subprocess artifact mapping are still kind/title heuristics rather than explicit process-output adapters.
7. Disposition routing can still convert own missing artifact production into a negative branch instead of blocking/recovering.
8. Operation contracts are inferred from text rather than persisted typed step fields.
9. Retry/no-progress logic is still partly in-memory and not durable across dispatcher restarts.
10. Lint is useful but not yet a strong enough publish/start readiness gate for high-criticality process definitions.

## Bundle Layout

- `inputs/` raw request and reviewed source observations
- `analysis/` verified findings and risk interpretation
- `requirements/` normalized requirements and invariants
- `architecture/` target runtime design
- `plan/` subbundle dependency map and phase gates
- `traceability/` requirement-to-subbundle map
- `shared-prompts/` implementation and QA prompts
- `subbundles/` execution-ready workstreams
- `proof/` planned proof manifests and semantic invariants
- `reviews/` self-review and execution report scaffold
- `scripts/` validation command notes
- `templates/` proof and subbundle templates

## Recommended Execution Order

1. `01-upstream-materialization-reactivation-transaction`
2. `02-lineage-keys-and-artifact-provenance-schema`
3. `03-script-tool-boundary-and-side-effect-policy`
4. `04-typed-grounding-sources-and-alias-trust`
5. `05-storage-backed-artifact-validation`
6. `06-explicit-workflow-subprocess-output-mapping`
7. `07-disposition-routing-ownership-guardrails`
8. `08-persisted-step-operation-contract-ui-and-import-export`
9. `09-durable-no-progress-ledger-and-active-run-reconciliation`
10. `10-lint-gates-and-red-team-closure`

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Implementation complete; full integration suite timed out`
- Subbundle gate review: `SB01-SB10 completed`
- Final closure gate: `Passed with residual full-integration timeout documented`
- Browser validation analytics: `Required only if SB08/SB10 changes process editor UI or launches browser-visible validation flows`
