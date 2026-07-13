# Process Tool Proof Readiness Refactor

## Profile

- `initiative`

## Mission

Prepare an implementation-ready architecture bundle that fixes process-step tool proof readiness without adding domain-specific behavior back into common MAF workspace tools. The target state is a typed process-owned channel for allowed, denied, suppressed, and required tools, skills, MCPs, scoped instructions, and proof receipts; HR readiness and manager fallback must use the same contract so process runs fail early or recover deliberately instead of looping on artifact-only attempts.

## Outcome Contract

- Requested outcome: analyze run `6f0d229f-7c7e-4322-8b73-614ba5910cc4`, identify why QA recheck blocked around browser/image proof, and implement phased process capability/proof contracts, readiness matching, fallback diagnostics, and template migration.
- Hard constraints: keep process/domain-specific instructions out of common MAF workspace plugins; preserve existing flexible tool architecture; avoid new bottlenecks, repeated catalog rebuilding, or unnecessary service reinstancing.
- Evidence required before closure: completed bundle validation passes; subbundles have exact source references, acceptance gates, C# architecture sections, proof manifests, semantic invariants, and command transcripts.
- Known blockers or explicit scope exceptions: live E2E process rerun is intentionally left to the restarted local 5032 instance so the user can retest the process run with new templates.

## Bundle Layout

- `inputs/` raw request, observed run data, and structured intent.
- `inventories/` affected runtime, template, agent, and process surfaces.
- `analysis/` current-state diagnosis, assumptions, and risks.
- `requirements/` normalized requirements with observable success criteria.
- `architecture/` target solution and C# boundary guard files.
- `plan/` phase order, architecture checkpoints, and validation gates.
- `traceability/` mapping from raw notes to subbundles and proof.
- `shared-prompts/` reusable execution and QA prompts.
- `subbundles/` numbered implementation workstreams.
- `reviews/` bundle self-review and execution report template.

## Recommended Execution Order

1. `subbundles/01-runtime-receipt-contracts`
2. `subbundles/02-hr-capability-readiness`
3. `subbundles/03-manager-fallback-drivers`
4. `subbundles/04-template-process-e2e`

## Dependency And Validation Map

- The receipt contract is the foundation. HR readiness and fallback logic must consume the same typed contract, not parallel prompt text.
- Template migration waits until the contract, readiness, and fallback surfaces exist.
- Every phase must preserve the rule that MAF composes generic capabilities and receipts while process templates and drivers own domain-specific requirements.

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Code and template proof completed; live E2E ready on restarted 5032 instance`
