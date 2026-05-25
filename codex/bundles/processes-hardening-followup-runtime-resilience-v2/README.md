# Processes Hardening Follow-up: Runtime Resilience, Boundary Truth, and Recovery Lineage

## Status

Completed.

## Branch Context

This bundle was prepared after reviewing the current `processes-hardening` branch. The user mentioned `process-hardening`, but the available GitHub branch is `processes-hardening`. Use the branch that exists in the repository unless the user creates a differently named branch before execution.

Current reviewed head:

- Branch: `processes-hardening`
- Commit message observed: `phase2`
- Commit SHA observed through GitHub connector: `e3410ca20e2038493fec50d0ac3d7c18cb723ccb`

## Mission

Harden the generic CanDoItAll process runtime so process steps do not block unnecessarily, do not retry the same no-progress failure, and do not allow an agent to perform work owned by a later step.

The runtime must remain generic. Do not turn the process core into a software-development-only workflow. Software delivery, Blazor apps, browser proof, .NET setup, and JavaScript launchers are important red-team scenarios, but the core solution must work for business, legal, research, manufacturing, operational, finance, HR, design, governance, and other process types.

## Key Current Finding

Codex made useful progress by adding:

- process-owned step completion finalizer
- execution boundary metadata
- read-only/mutable external target aliases
- workflow/subprocess finalizer routing improvements
- artifact validation diagnostics
- upstream materialization request records
- a process definition linter

The remaining problem is not a lack of instructions. The core risk is that important runtime decisions are still made from broad text heuristics, prompt wording, mutable candidate state, and partial metadata. That can still cause:

- architecture/planning steps to be classified as product mutation because they say "create architecture artifact"
- valid business/research artifacts to be blocked by software-oriented validation words
- manager recovery artifacts to be rejected as stale because they belong to the recovery execution run, not the original execution run
- workflow/subprocess steps to pass or block depending on projection luck instead of typed artifact adapter output
- downstream steps to remain blocked after upstream materialization succeeds
- negative branch routing to hide missing required artifacts on non-review steps
- no-progress retry compression to miss repeated invalid evidence because each attempt wrote "something"

## Bundle Layout

- `inputs/` raw request, reviewed source observations, and structured input
- `analysis/` verified findings and risk interpretation
- `requirements/` normalized requirements and invariants
- `architecture/` target runtime architecture
- `plan/` execution sequence, dependency map, and gates
- `traceability/` requirement-to-subbundle map
- `shared-prompts/` implementation and QA prompts
- `subbundles/` execution-ready workstreams
- `proof/` planned proof manifests and semantic invariant placeholders
- `reviews/` self-review and execution report scaffold
- `scripts/` validation command notes

## Recommended Execution Order

1. `01-explicit-step-operation-contract-and-classifier-hardening`
2. `02-tool-policy-boundary-enforcement-and-metadata-no-autopromotion`
3. `03-manager-recovery-lineage-and-recovery-artifact-validation`
4. `04-workflow-subprocess-artifact-adapters-and-parent-versioning`
5. `05-upstream-materialization-unblock-and-resume-lifecycle`
6. `06-disposition-routing-guardrails`
7. `07-storage-backed-artifact-validation-and-explicit-modes`
8. `08-no-progress-retry-and-active-run-adoption-hardening`
9. `09-process-definition-lint-integration-and-template-quality-gates`
10. `10-generic-red-team-validation-suite`

## Non-Negotiable Constraints

- Keep `Processes` above `Workflows`. Workflows belong to the agent/workflow module and can be assigned as process role executors, but process step contracts, artifacts, dispositions, and transitions are process-owned.
- Do not reintroduce SQLite work. PostgreSQL is the canonical runtime database.
- Do not solve scope drift only with prompt text.
- Do not use branch-specific hardcoding for Blazor, .NET, or JavaScript in generic runtime decisions.
- Do not weaken process artifacts into chat summaries.
- Do not allow a required artifact to be satisfied by a placeholder, stale artifact, or unrelated workflow/subprocess output.
- Do not block review/approval steps when a modeled repair/no-go/escalation disposition is the correct process outcome.
- Do not route missing artifact production steps to repair/no-go branches just because a negative branch exists.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Completed for SB01-SB10`
- Final closure gate: `Completed with source, unit, integration, build, PostgreSQL-only audit, and bundle validator proof`
- Browser validation analytics: `No runtime UI launch was required; SB10 red-team validation used source, unit, and integration proof for generic software and non-software scenarios`

