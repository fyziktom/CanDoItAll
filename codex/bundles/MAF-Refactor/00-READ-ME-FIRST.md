# CanDoItAll Agent Runtime, Floating Context, MAF Boundary, and Lightweight LLM Refactor — Claude/Fable 5 Bundle v2

## Bundle identity

- Repository: `fyziktom/CanDoItAll`
- Target branch: `development`
- Analyzed baseline: `51d9a2f071e9a5f295abac884c8c667328462cc4`
- SharedInfo baseline: `67a5e73a6f80ae3d7c8afcee56f9e7cde48b5939`
- Target executor: Claude Code
- Preferred model: Claude Fable 5
- Reasoning profile: maximum/deep available (`xHigh` intent; no literal Claude CLI flag is assumed)
- Fallback: Claude Opus 5 when that model is configured and available in the operator environment; otherwise the best available high-capability Claude model with durable handoff
- Bundle date: 2026-08-06
- Bundle language: English
- Change type: multi-phase C# architecture refactor with blocking checkpoints and stabilization phase

## Recommended reading order

1. `02-ARCHITECTURE-SUMMARY.md`
2. `05-REVISION-NOTES-AND-CHANGE-IMPACT.md`
3. `04-CLAUDE-CODE-EXECUTION-GUIDE.md`
4. `architecture/05-canonical-context-model.md`
5. `architecture/09-practical-floating-agent-contract.md`
6. `architecture/11-change-impact-and-adaptation-map.md`
7. `architecture/12-high-risk-cutover-playbook.md`
8. `architecture/13-post-refactor-debugging-and-bugfixing.md`
9. `architecture/14-lightweight-llm-and-ordinary-chat-foundation.md`
10. `architecture/15-exact-code-adaptation-inventory.md`
11. ADRs 001–011
12. `plan/architecture-checkpoints.md`, cutover/rollback, and validation plans
13. The selected subbundle README and `CLAUDE-CODE-PROMPT.md`
14. `claude/REGRESSION-BUGFIX-PROMPT.md` when diagnosing a migration regression
15. `claude/FINAL-ARCHITECTURE-REVIEW-PROMPT.md` for an independent SB17/SB18 review

## Mission

Refactor CanDoItAll so that:

1. Floating agent chats follow the user's current application surface without losing conversation continuity.
2. Canvas -> Gantt and stronger Project X -> Y transitions are visible to the next explicit turn.
3. A running turn and every approval continuation retain the exact context and authority captured at admission.
4. UI observation, conversation affinity, turn context, execution authority, product state, execution state, and runtime adapter state each have one explicit owner.
5. Every execution uses one coherent scope-bound workspace service bundle.
6. The broad `IAgentRuntime` is split into SDK-free narrow ports.
7. MAF becomes a thin adapter with no product-module references or process semantics.
8. Process recovery/provider/completion policy is owned by Processes and runs ordinary completion gates exactly once.
9. Persisted runtime state is versioned and compatibility is explicit.
10. Approvals are decided per stable proposal ID.
11. Ordinary workflow LLM calls use a provider-backed lightweight inference port.
12. The same lightweight port can support a future ordinary LLM chat without treating it as an agent.
13. Every risky cutover has characterization, a single production path, telemetry, rollback, fault tests, and a deletion owner.

## Functional interpretation of floating agents

A floating agent is a long-lived conversation overlay above the current UI. Live observation changes immediately, but a running turn is immutable. Switching Canvas -> Gantt changes the next turn; it does not rewrite the active run. Switching Project X -> Y creates a stronger context epoch and requires new authority. An approval created under Project X remains bound to Project X even while the user views Y.

## One authority per concern

| Concern | Authoritative owner |
|---|---|
| Project/task/process data | product module and canonical persistence |
| Current user attention/visible facts | live UI observation registry |
| What a thread follows | conversation context affinity |
| Facts supplied to one turn | immutable turn context snapshot |
| What the turn may access | execution authority snapshot |
| Scope-bound workspace behavior | execution-owned service bundle |
| Run/approval/receipt/artifact state | execution run store/governance |
| Provider continuation payload | versioned runtime adapter envelope |
| Provider inference without agents | lightweight LLM port/provider runtime |
| MAF SDK mapping | MAF adapter only |

## Existing behavior to preserve

- atomic context publication and monotonic revisions;
- navigation/route fencing;
- fail-closed loading/stale/unauthorized context;
- digest-bound transient context retained through approval;
- bounded/fresh opaque attachments;
- completion refresh to originating source;
- provider usage, tool/finalizer traces, receipts, and cleanup ownership;
- primary failure preservation during disposal;
- process completion gates and evidence authority.

## Mandatory operating rules

1. Execute subbundles in dependency order and honor checkpoint decisions.
2. Use one subbundle per Claude session/branch where practical.
3. Use SharedInfo skills and CodeAnalytics MCP; verify exact source and project files.
4. Add characterization/failing-first tests before moving behavior.
5. Do not add partial-class architecture, nested owners, broad helpers/managers, service location, or Common dumping grounds.
6. Do not create reverse references or cycles.
7. Do not let UI/payload data grant authority.
8. Do not recapture current UI during continuation.
9. Do not mix workspace service bundles in one execution.
10. Do not shadow-execute side effects.
11. Do not put product/process behavior in MAF.
12. Do not silently reset/migrate incompatible runtime state.
13. Do not route lightweight LLM calls through agent execution.
14. Fix defects in the owning layer with a regression test first.
15. Persist durable proof and handoff before changing model/session.

## First actions for Claude Code

1. Verify current `development` HEAD and compare it to the analyzed baseline.
2. Review existing repository `CLAUDE.md`; merge only missing bounded rules from `claude/CLAUDE.bundle.template.md`.
3. Verify CodeAnalytics MCP and installed SharedInfo skills.
4. Read the required root/revision/cutover documents.
5. Execute SB00 only.
6. Produce baseline proof and the first unlock decision.

## Completion definition

The bundle is complete only when all context, scope, runtime, MAF, process, continuation, workflow, lightweight-LLM, API, lifecycle, fault-injection, dependency, and release gates pass; production has one owner/path per responsibility; compatibility retained is explicit and safe; and SB18 records the final architecture decision.
