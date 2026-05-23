# Process Browser Evidence And Runtime Proof Hardening

This bundle is a coordination and execution package for hardening generic process automation so UI/runtime quality gates cannot pass on prose-only browser proof.

## Profile

- `initiative`

## Mission

Make the multi-team software-delivery process reject shallow browser proof. When a process step requires runtime or browser evidence, screenshots, DOM or snapshot output, console diagnostics, and representative interaction assertions must be durable process artifacts, validated before release readiness, and surfaced as conformance observations when missing or invalid.

## Outcome Contract

- Requested outcome: repair the generic process/runtime evidence path exposed by development DB run `4f218d64-2cb3-49fc-ad00-fc7dba917f79`, where QA accepted a TetrisGame browser proof even though process artifact records had no screenshot or browser artifacts and the raw console log later showed JavaScript/Blazor connection errors.
- Hard constraints: keep process core generic; do not hardcode Tetris, Blazor, or canvas-specific product rules in process runtime; domain acceptance belongs in project structure context, process step definitions, and agent instructions.
- Evidence required before closure: targeted unit/integration tests, a failing-first fixture matching the DB failure shape, process artifact records for browser screenshot/snapshot/console evidence, conformance observations for missing or invalid evidence, and a fresh clean-development-DB process run with process-visible Playwright MCP screenshot evidence.
- Known blockers or explicit scope exceptions: this preparation bundle does not repair the generated Tetris app itself and does not reset the development database; clean DB setup belongs to the execution phase after code changes are ready.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-browser-evidence-contract-and-storage`
2. `subbundles/02-generic-runtime-proof-gates`
3. `subbundles/03-process-definition-agent-instruction-contracts`
4. `subbundles/04-regression-and-demo-readiness-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared validator passed`
- Execution status: `Code-level implementation complete; live clean-DB process retest left to user`
- Subbundle gate review: `SB01-SB03 completed; SB04 regression and DB reset complete, live process artifact proof pending user retest`
- Final closure gate: `Partial - code and clean DB ready, no fresh multi-agent run executed after reset`
- Browser validation analytics: `Fixture and contract proof passed; live browser artifacts pending fresh user run`
