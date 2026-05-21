# Office365 Email Summary Project Scope Fix

This bundle closes the Office365 email summary workflow failure where the LLM node could fetch the categorized email but failed before writing the summary asset because Cognitive Memory received no project-scoped context.

## Profile

- `feedback`

## Mission

- Preserve the governed Cognitive Memory boundary while allowing project-structure workflows to pass their project scope into MAF LLM execution and complete on newly created projects with no memory records yet.

## Outcome Contract

- Requested outcome: Office365 category workflow fetches the categorized client email, summarizes it with the LLM, and creates a markdown asset under the workflow node that started the workflow.
- Hard constraints: keep project scope explicit; do not silently ignore missing project scope; keep actual Cognitive Memory outages failing; do not weaken project-structure lease checks.
- Evidence required before closure: targeted unit tests, API-level integration test, live `candoitall_development` Office365 workflow run, and proof that the created markdown asset is a child of the workflow node.
- Known blockers or explicit scope exceptions: no UI changes; no browser screenshot pass required.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `proof/` command transcripts, live-run evidence, semantic invariant contracts, and proof manifests
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-propagate-workflow-project-scope-to-agent-context`
2. `subbundles/02-verify-office365-email-summary-creates-project-asset`

## Dependency And Validation Map

- SB01 is the foundation: MAF context contributors must receive the workflow project scope before any live Office365 verification is meaningful.
- SB02 depends on SB01 and proves the full Office365 workflow path in the development database.

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `N/A - backend/API workflow only`
