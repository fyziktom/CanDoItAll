# mcp-dotnetwatch-improvement-bundle-1

This bundle is the planning package for the next redesign pass of `CanDoItAll.Mcp.DotNetWatch`.
It is based on:

- `implementation-phase2/DOTNETWATCH_ANALYSIS.md`
- the current `CanDoItAll.Mcp.DotNetWatch` source tree
- the existing wrapper/bootstrap path
- the current unit and integration test surface

This bundle is plan-only. It does not authorize ad hoc implementation outside the documented phases, gates, and prompts below.

## Intent

The redesign must solve five problems together:

1. Make direct Codex-to-MCP calls trustworthy again.
2. Preserve fluent live-edit workflows for small source changes.
3. Add a true Codex-safe atomic update path that does not depend on reusing one hot publish folder.
4. Keep the detached backend architecture and current watch/health strengths that already work.
5. Steer Codex toward small validated iterations instead of broad unverified edit batches.

## Core decisions

1. Keep the detached backend. The current backend/session model is a strength, not a target for removal.
2. Split the runtime into explicit lanes: bridge/control plane, source-watch lane, build-test lane, atomic runtime lane, and shadow-host lane.
3. Add published-artifact and executable launch support instead of forcing everything through `WatchRun` or `RunOnce`.
4. Define atomicity as control-plane atomicity for Codex:
   the current active runtime remains authoritative until a candidate runtime is healthy and committed.
   Stable public ports without a relay/proxy are out of scope for bundle 1.
5. Replace single-folder publish flows with slot-based isolated runtime artifacts.
6. Replace the current coarse workspace mutation gate with resource-scoped coordination.
7. Add a compact workflow-steering layer:
   tool descriptions teach the preferred iteration pattern once, and selected status/control responses emit tiny state-based guidance without polluting logs or event streams.

## Reading order

1. `01-current-state-analysis.md`
2. `02-target-operating-model.md`
3. `03-architecture-redesign.md`
4. `04-tool-contract-and-state-model.md`
5. `05-implementation-plan.md`
6. `06-checklists.md`
7. `07-prompts.md`
8. `08-validation-criteria.md`
9. `09-risk-register.md`
10. `10-qa-gap-review-round-1.md`
11. `11-qa-remediation-summary-round-1.md`
12. `13-qa-gap-review-round-2.md`
13. `14-qa-remediation-summary-round-2.md`
14. `12-final-qa-signoff.md`

## Expected outcome

After implementation of this bundle, Codex should be able to:

- call the MCP server without generic transport-style failures
- choose the correct execution lane for the task
- use `dotnet watch` as the fast path for small changes
- stay on a one-nearby-change, revision-confirm, browser-check loop when the watch lane is healthy
- use a slot-based publish path for atomic validation or handoff
- observe revisions, transactions, slots, and rollbacks through structured tool responses
- recover from bridge/backend churn without losing the authoritative runtime state
