# MAF Runtime Deep Isolation Phase 2

This initiative bundle prepares the next MAF runtime refactor phase. It is intentionally stricter than the previous bundle: the goal is not to move a few helpers, but to stop using `MafAgentRuntime` as a hidden namespace for private builders, configuration DTOs, policy helpers, plugins, and execution utilities.

## Profile

- `initiative`

## Mission

Make `MafAgentRuntime` a thin `IAgentRuntime` adapter and move the remaining hidden responsibilities into named, independently testable runtime components. The target design must eliminate private nested builders and DTOs from the runtime, reduce partial-class surface area, and give unit/integration tests direct seams for capability composition, MCP, workspace tools, input attachments, finalizer/recovery behavior, and session persistence.

## Outcome Contract

- Requested outcome: prepare a new implementation-ready bundle for a deeper MAF runtime isolation phase; do not implement production changes in this turn.
- Hard constraints: no Financial Strategist, quotation, margin, MarkItDown, or domain-specific agent feature work; preserve current behavior; avoid dumping new classes back under `MafAgentRuntime`; introduce interfaces only for real DI or test boundaries; prefer internal sealed collaborators and strongly typed request/result records.
- Evidence required before closure: current-state nested-type inventory, target responsibility map, dependency-aware subbundles, direct source references, architecture guard requirements, unit/integration proof plans, performance/startup proof plan, and validator-passing bundle.
- Known blockers or explicit scope exceptions: this bundle prepares implementation only. It does not fix unrelated full-suite baseline failures from cognitive-memory, project-structure, CRM/resource, migration bootstrap, or repository hygiene areas.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input.
- `analysis/` repo-grounded current state, root cause, assumptions, risks, and reopen triggers.
- `requirements/` normalized, testable requirements.
- `architecture/` target solution and maintainability boundaries.
- `inventories/` exact MAF runtime partial, nested type, and test-dependency inventory.
- `plan/` execution order, dependency graph, critical subbundles, and phase gates.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `proof/` planned proof manifest and semantic-invariant skeletons for critical phases.
- `reviews/` self-review and execution report seed.

## Recommended Execution Order

1. `subbundles/01-01-current-state-hidden-runtime-map`
2. `subbundles/02-02-runtime-contracts-and-configuration-dtos`
3. `subbundles/03-03-capability-composition-coordinator`
4. `subbundles/04-04-capability-builder-extractions`
5. `subbundles/05-05-workspace-input-and-artifact-drivers`
6. `subbundles/06-06-execution-finalizer-and-recovery-drivers`
7. `subbundles/07-07-test-harness-and-architecture-guards`
8. `subbundles/08-08-performance-and-final-closure`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` as the source of truth for sequencing and gates.
- SB01-SB04 are critical foundations. Downstream phases must not start unless their semantic gates pass.
- Any extracted collaborator must have direct unit tests, production caller proof, and an anti-stub audit.
- Any new runtime state, diagnostic, measurement, or contract must include a Production Behavior Artifact Matrix in critical proof.
- Browser validation is `N/A` unless implementation adds browser-visible runtime diagnostics. Host-level proof is required for workspace/MCP process-driver behavior.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented with focused proof`
- Subbundle gate review: `Focused closure passed with explicit SB06/SB08 residuals`
- Final closure gate: `Focused runtime proof passed; full repository suite not run`
- Browser validation analytics: `N/A for planned backend refactor unless UI-visible diagnostics are added`
