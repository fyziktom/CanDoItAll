# Git Wrapper Agent Tools and Skill

This bundle coordinates the git wrapper, agent runtime git tools, and the agent-facing skill/capability updates needed for standard local git operations.

## Profile

- `initiative`

## Mission

Improve the existing `CanDoItAll.Git` wrapper so git command construction is strongly typed and reusable, then expose a bounded local git tool set to app-managed agents with template-backed skill guidance. The result must let agents inspect status/diff/log/show, stage and unstage paths, commit, create branches, and switch branches without raw ad hoc git argument strings spread across the runtime.

## Outcome Contract

- Requested outcome: app-managed agents receive a coherent, policy-classified git tool surface and a complementary inline skill that explains safe standard local git workflows.
- Hard constraints: no push, pull, fetch, reset, checkout, rebase, remote management, credential handling, silent fallback behavior, shell-built commands, or stringly typed tool identifiers where constants already exist or can be added.
- Evidence required before closure: prepared-stage bundle validation, subbundle gate rows, changed-file hashes, source assertions, anti-stub audit, focused unit/integration tests, and final `dotnet test` proof for affected projects.
- Known blockers or explicit scope exceptions: remote/network git operations are out of scope; merge operations remain wrapper-only unless a later process contract explicitly needs conflict-resolution tooling.

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

1. `subbundles/01-sb01-git-wrapper-architecture-foundation`
2. `subbundles/02-sb02-agent-runtime-git-tools`
3. `subbundles/03-sb03-agent-git-skill-and-capability-guidance`
4. `subbundles/04-sb04-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Execution status: `Complete`
- Subbundle gate review: `All subbundles closed`
- Final closure gate: `Completed validator passed`
- Browser validation analytics: `N/A - non-UI runtime/tooling change`
