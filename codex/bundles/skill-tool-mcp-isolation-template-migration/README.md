# Skill Tool MCP Isolation And Template Migration

This initiative bundle prepares the long-run migration to isolate Skills, Tools, and MCP servers from the current MAF wrapper and seed code while preserving existing agent, workflow, and process behavior.

## Profile

- `initiative`

## Mission

Create implementation-ready workstreams for a file-driven capability system where skills, tools, and MCP servers have explicit abstractions, dedicated implementation projects, template-backed catalog definitions, typed access restrictions, setup/test flows, and regression proof across MAF execution, agent templates, processes, workflows, and UI configuration.

## Outcome Contract

- Requested outcome: prepare, not implement, the migration bundle for better isolation and templating of skills, tools, and MCP servers.
- Hard constraints: no production implementation in this preparation phase; start with new projects and harden them before reconnecting MAF; keep UI/application/domain/infrastructure boundaries explicit; keep existing capability keys, runtime tool names, and process/workflow operation behavior compatible.
- Evidence required before closure: bundle validator passes at prepared stage, every raw request is traceable to a subbundle, phase gates cover unit/integration/e2e tests, and the planning workbook is generated and visually checked.
- Known blockers or explicit scope exceptions: no production code changes are included here; exact project names remain proposed until implementation validates dependency direction against `CanDoItAll.slnx`.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input.
- `analysis/` current-state findings, assumptions, risks, and reopen triggers.
- `requirements/` normalized requirements and naming compatibility rules.
- `architecture/` target solution, reconnection map, diagnostics model, quality guardrails, and capability access policy.
- `inventories/` current surfaces, hardcoded seams, and test inventory.
- `templates/` proposed template pack shape for `Templates/Capabilities`.
- `plan/` subbundle order, dependency map, critical gates, and validation gates.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` preparation review and execution report seed.
- `proof/` expected future proof structure for critical subbundles.

## Recommended Execution Order

1. `subbundles/01-contracts-and-template-schema`
2. `subbundles/02-tooling-abstractions-and-implementations`
3. `subbundles/03-skills-abstractions-and-loader`
4. `subbundles/04-mcp-abstractions-and-runtime`
5. `subbundles/05-capability-core-hardening-checkpoint`
6. `subbundles/06-template-loading-and-seeding`
7. `subbundles/07-template-seed-hardening-checkpoint`
8. `subbundles/08-maf-reconnection-and-compatibility`
9. `subbundles/09-runtime-hardening-and-optimization-checkpoint`
10. `subbundles/10-ui-api-setup-and-test-flows`
11. `subbundles/11-regression-proof-for-processes-workflows`
12. `subbundles/12-cleanup-hardening-and-docs`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, the workbook, and `reviews/01-execution-report.md` as durable state.
- Critical subbundles require semantic adequacy proof and artifact-backed manifests under `proof/SBxx/` before dependent work starts.
- Checkpoint subbundles SB05, SB07, and SB09 are mandatory hardening gates. They must refactor overgrown files, tighten diagnostics, run focused performance scans, and block the next phase if the migration is only happy-path correct.
- Capability restrictions for agents, processes, workflows, and UI must use the shared typed access policy/effective-set model. MAF must not keep separate hidden skill/tool/MCP suppression rules after reconnection.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed - SB01 through SB12 completed`
- Subbundle gate review: `SB01-SB12 closure passed`
- Final closure gate: `Passed`
- Browser validation analytics: `SB10 and SB11 large-screen proof passed; small and medium viewport tests skipped per user instruction`
