# MAF Runtime Architecture Isolation

This initiative bundle prepares a staged refactor of `MafAgentRuntime` so it becomes a thin runtime coordinator instead of a huge partial-class feature sink. It intentionally removes the earlier Financial Strategist quotation/margin/writeback scope. Agent-specific cases can resume later after the generic runtime base is isolated, measurable, and testable.

## Profile

- `initiative`

## Mission

Split `MafAgentRuntime` by real responsibilities: runtime orchestration, capability composition, access planning, tool-provider composition, provider client construction, session execution, finalization, workspace integration, MCP integration, context/skill drivers, diagnostics, and performance instrumentation. The refactor must improve maintainability and testability without a risky big-bang rewrite.

## Outcome Contract

- Requested outcome: implement the repaired generic MAF runtime architecture bundle after scope correction.
- Hard constraints: focus strictly on generic MAF runtime architecture; remove Financial Strategist, margin calculation, document-domain, and project-structure writeback implementation work from this bundle; preserve current behavior while extracting responsibilities; avoid silent fallback mechanisms that hide missing dependencies; keep strongly typed request/result contracts; keep changes staged and reversible.
- Evidence required before implementation closure: responsibility map, baseline tests, direct unit tests for extracted collaborators, integration tests with mocked providers/tool providers/context contributors, performance baseline and after-change measurements, architecture boundary assertions, and final behavior parity proof.
- Explicitly deferred: Financial Strategist PDF/MarkItDown/tool reachability, quotation extraction, margin calculation, and project-structure writeback are future bundles after the MAF runtime base is stable.

## Bundle Layout

- `inputs/` current raw scope correction, source artifacts, and structured input.
- `analysis/` repo-grounded current-state findings, assumptions, risks, and performance scan.
- `requirements/` normalized generic runtime architecture requirements.
- `architecture/` target responsibility split and Microsoft Learn grounding.
- `inventories/` source inventory for MAF runtime partials, collaborators, tests, and performance seams.
- `plan/` execution order, dependency map, critical foundations, and phase gates.
- `traceability/` raw-note and requirement coverage.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` bundle self-review and execution report seed.

## Recommended Execution Order

1. `subbundles/01-maf-runtime-current-state-and-responsibility-map`
2. `subbundles/02-runtime-contracts-and-composition-root`
3. `subbundles/03-capability-composition-and-tool-provider-extraction`
4. `subbundles/04-provider-build-session-and-finalizer-drivers`
5. `subbundles/05-workspace-mcp-context-skill-and-tool-drivers`
6. `subbundles/06-test-harness-and-integration-mockability`
7. `subbundles/07-performance-regression-and-architecture-closure`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` as the source of truth for sequencing and gates.
- Critical subbundles require semantic adequacy proof and artifact-backed manifests under `proof/SBxx/`.
- Every extracted collaborator must have a production caller, direct tests, and an anti-stub audit.
- Any new runtime state, diagnostic, measurement, or contract must include a Production Behavior Artifact Matrix in critical proof.
- Browser proof is not required for this backend refactor unless execution adds UI-visible diagnostics.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented core runtime seams; residual feature-driver extraction remains`
- Subbundle gate review: `SB02/SB03/SB04/SB06 core gates passed; SB05/SB07 partial`
- Final closure gate: `Partial closure, not full architecture elimination`
- Browser validation analytics: `N/A unless implementation adds UI-visible runtime diagnostics`
