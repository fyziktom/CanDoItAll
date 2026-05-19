# Agent Teams Management And HR Matching

This bundle coordinates implementation for agent teams, team-scoped agent filtering, team membership management, and process launch HR matching that can use a selected delivery team.

## Profile

- `initiative`

## Mission

- Add AgentFramework-owned agent teams that can contain multiple agents, allow one agent to belong to multiple teams, expose team and membership management on the `/agents` Agents tab, and let process launch HR matching prefer a selected team while marking candidates that are outside the selected team.

## Outcome Contract

- Requested outcome: agents can be grouped into reusable teams, the agents catalog has a tree view that filters by team, team creation and membership editing live in the Agents module, the membership modal supports multi-select cards like the chat switch-agent modal, and process launch matching can run with a selected delivery team.
- Hard constraints: AgentFramework remains the canonical technical agent runtime owner; CRM-HR stays a projection and staffing surface; an agent can be in multiple teams; process HR matching must not hide required roles that need out-of-team candidates.
- Evidence required before closure: targeted component/integration tests for team persistence and matching semantics, successful build/test command, browser proof for `/agents?tab=agents`, open-state proof for the add-agents-to-team modal, and integration proof for process launch HR matching with team selection and out-of-team markers. Process browser proof was attempted but blocked by local SQLite lock contention in the development host.
- Known blockers or explicit scope exceptions: no long-lived database schema should be added for AgentFramework teams unless implementation proves the file-backed catalog cannot safely hold team definitions; teams are technical-agent teams, not CRM-HR delivery units.

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

1. `subbundles/01-01-agent-team-domain-and-service`
2. `subbundles/02-02-agents-page-tree-and-team-management-ui`
3. `subbundles/03-03-process-hr-team-scoped-matching`
4. `subbundles/04-04-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Implemented with noted browser-proof exception`
- Subbundle gate review: `01 completed; 02 completed; 03 completed; 04 completed with SQLite browser blocker noted`
- Final closure gate: `Passed with documented browser limitation`
- Browser validation analytics: `Agents page captured; process launch selector covered by integration/build proof due SQLite lock blocker`
