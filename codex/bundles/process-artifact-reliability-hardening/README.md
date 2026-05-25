# Process Artifact Reliability Hardening

## Profile

- `initiative`

## Mission

Harden `CanDoItAll.Modules.Processes` so process steps do not get stuck in repeated retries when required artifacts are missing, malformed, stale, or not evidence-bound. The Processes runtime must own the final artifact contract check across every executor kind, including direct AgentFramework agent execution and workflow-backed role execution, without moving process semantics into the Agents workflow module.

## Outcome Contract

- Process steps complete only after their required process artifact expectations are satisfied by valid, current-run, evidence-bound artifacts.
- Missing or invalid artifacts produce actionable diagnostics and either targeted manager recovery or a blocked state with exact evidence gaps, not blind repeated executor retries.
- Workflow-backed roles are supported through the Processes runtime boundary: workflow execution may produce work, but Processes validates process artifact expectations and owns step finalization.
- Recovery artifacts are structurally distinguishable from primary agent artifacts and carry provenance back to the execution run, recovery decision, source evidence, and validation outcome.
- SQLite is out of scope. The current development branch is PostgreSQL-only after `db-remove-sqlite`; do not add SQLite migrations, SQLite tests, or provider-switching work.

## Grounded Current State

This bundle was prepared from the `development` branch at commit `62dfbdd68bc84cd74f852f3e40a5f42a2183174c` (`Merge branch 'db-remove-sqlite' into development`). The source review confirms these key facts:

- `CanDoItAll.Modules.Processes` is the canonical process runtime module for templates, process runs, step transitions, work briefs, governed outcomes, artifacts, and AI-agent dispatch.
- Processes can route execution through AgentFramework agents or workflow-backed role execution, but process artifact semantics belong in the Processes module.
- Direct AgentFramework execution currently projects artifacts and attempts completion-artifact recovery before transitioning the process step.
- Workflow-handled execution currently transitions the step through `HandleWorkflowExecutionOutcomeAsync` without the same process-owned artifact projection/recovery/finalization path.
- Manager artifact recovery exists and is valuable, but it still depends on mutable in-memory candidate sets and fuzzy manager fallback selection.
- Artifact projection supports many paths, including execution artifacts, workspace writes, existing managed files, final assistant response text, provider-native browser artifacts, and auto-decision artifacts. This is powerful but needs stronger artifact-mode validation and diagnostics.

## Bundle Layout

- `inputs/` raw request, structured input, and source observations
- `analysis/` current-state analysis, verified findings, risks, and retry failure interpretation
- `requirements/` normalized requirements and process/workflow boundary rules
- `architecture/` target design for step finalization, artifact validation, recovery, provenance, and PostgreSQL-only execution
- `inventories/` source and risk inventories
- `plan/` dependency-aware phase plan
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` execution-ready workstreams
- `proof/` planned proof manifest templates for critical subbundles
- `reviews/` preparation self-review and execution report scaffold
- `templates/` reusable subbundle/proof templates
- `scripts/` validation command notes

## Recommended Execution Order

1. `subbundles/01-process-owned-step-completion-finalizer`
2. `subbundles/02-artifact-contract-validation-and-diagnostics`
3. `subbundles/03-evidence-bound-manager-recovery`
4. `subbundles/04-projection-provenance-and-placeholder-safety`
5. `subbundles/05-retry-blocking-and-stranded-step-hardening`
6. `subbundles/06-postgresql-only-validation-and-red-team-suite`

## Critical Foundation Subbundles

- `SB01` is critical because every executor kind must pass through the same process-owned finalizer before any downstream transition is trustworthy.
- `SB02` is critical because artifact existence must be separated from artifact validity, freshness, evidence completeness, and schema/format compliance.
- `SB03` is critical because manager recovery must repair missing artifacts without inventing evidence or silently completing invalid steps.

## Validation Summary

- Bundle preparation status: `Ready for Codex execution`
- Execution status: `Not executed by this bundle`
- Final closure gate: `Pending implementation`
- Browser validation analytics: `N/A for preparation; required only for UI/browser-proof scenarios created during execution`

Use the repository bundle workflow skill (`codex/skills/bundles/candoitall-bundle-workflow/SKILL.md`) to execute this bundle one subbundle at a time.
