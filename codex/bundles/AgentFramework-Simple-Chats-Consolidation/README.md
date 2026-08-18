# AgentFramework Simple Chats Consolidation

## Outcome

This initiative consolidates Simple Chats with the AgentFramework product surface without moving the feature wholesale into the Agent module.

The target state is:

- Simple Chat domain, runtime, persistence, and reusable UI code are MAF libraries under src/MAF/SimpleChats.
- Existing generic CanDoItAll.AgentFramework.Llm.* libraries remain the low-level LLM abstractions and helpers.
- CanDoItAll.Modules.AgentFramework owns the product page composition: Simple Chats is a peer tab immediately after Agents.
- /chats remains a compatibility route that redirects to the canonical AgentFramework tab without duplicating a page or navigation item.
- Agent and Simple Chat costs are projected through one typed analytics contract with Agents, Simple Chats, and Both selections.
- Agent execution evidence and relational Simple Chat invocation attempts remain their respective authoritative audit stores.
- Existing Agent and Simple Chat main/floating chat behavior remains intact.
- The Simple Chat definition dialog uses Identity, Runtime, and Output and revision settings tabs and shares the full Agent avatar selector, including AI generation, instead of exposing an avatar URL textbox.

The bundle has been implemented. Product, migration, architecture, focused-test, named Playwright, and Playwright MCP evidence are recorded under `proof/` and `reviews/`.

## Execution Verdict

Implementation complete. Architecture, focused selectors, named Playwright, and Playwright MCP pass. The one authorized Stable run discovered eight stale test-only contracts: six component fixtures omitted the new avatar gateway and two source-boundary assertions still described the old route/path. Those exact classes pass after repair. Stable was not rerun because the bundle authorizes exactly one broad run; FINAL certification is therefore conditional until an explicitly authorized second Stable run.

The bundle deliberately rejects:

- copying the existing LlmChats projects into CanDoItAll.Modules.AgentFramework;
- dual-writing Simple Chat usage into the Agent file store;
- merging usage only inside Razor components;
- query-time repricing of historical calls;
- treating a Simple Chat definition or conversation as an Agent.

## Target Project Shape

- src/MAF/Common/CanDoItAll.AgentFramework.Usage
- src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Core
- src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Application
- src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime
- src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence
- src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Components
- src/Modules/CanDoItAll.Modules.AgentFramework remains the page and product composition owner.

No feature-level SimpleChats.Abstractions project is planned. Existing AgentFramework.Llm.Abstractions, AgentFramework.Llm.Conversations, and AgentFramework.Llm.ProviderRuntime already own the generic seams; a new empty abstraction layer would duplicate them. SimpleChats.Application owns its narrow ports. Reopen that decision only if SB01 proves a real independent consumer contract that cannot live in Core or Application.

## Execution Order

1. SB01 freezes behavior and validates the architecture contract.
2. SB02 establishes typed cross-workload usage analytics.
3. SB03 moves the Core and Application layers as a single compile-safe extraction; SB04-SB05 split runtime and persistence.
4. SB06 composes Agent and Simple Chat usage projections.
5. SB07-SB09 move reusable UI, integrate the Agent page, and scope the dashboard.
6. SB10 removes legacy project/namespace/composition residue.
7. SB11 runs the one broad Stable gate and named Playwright/Playwright MCP closure.

Execution must stop at every checkpoint. Later phases may not be pulled forward merely to make an intermediate build easier.

## Baseline

- Repository: fyziktom/CanDoItAll
- Branch: simple-chats-agent-module
- Prepared head: 30edf7b034cb2a06d29ee3ba2df8193006109dd5
- SharedInfo head: 7b7808e8591d7219f40826cf0e5624e182981d90
- Fresh scoped CodeAnalytics snapshot: snap-20260817172927-da2eea1a
- Worktree was clean before bundle preparation.

## Proof Policy

Every subbundle owns focused tests, impact analysis, an execution report, and an evidence manifest. Architecture-changing phases are Governed. Only SB11 may run the unfiltered Stable solution, exactly once against a frozen candidate. Full Playwright is not authorized; the named consolidation scenarios and Playwright MCP browser proof are required.

## Subbundle Gate Results

SB01-SB11 implementation is complete. CP0-CP4 pass. FINAL is conditional only on the one-shot Stable exception described above; no product defect remains open.

## Browser Validation Analytics

Pass at 1600x1000. The real UI proved main and floating Agent/Simple Chat conversations, hide/reopen continuity, the `/chats` compatibility redirect, route persistence, scope-exact dashboard totals/dialogs/charts, the three-tab Simple Chat settings dialog, shared avatar selection/upload, and configured-provider AI image generation. Six screenshots and SHA256 hashes are recorded in `proof/SB11/playwright-mcp-evidence.md`; console/page errors were zero.

## Raw Input Closure

Every raw request, including the follow-up for internal settings tabs and shared AI avatar generation, is implemented and mapped to the closure proof.

## Validation Summary

- Bundle preparation status: Complete
- Execution status: Complete
- Local structural/traceability/test-policy/checksum validation: Pass
- Canonical initiative readiness validation: Pass
- C# architecture preparation gate: Pass
- Subbundle gate review: SB01-SB11 implemented; CP0-CP4 pass
- Final closure gate: Conditional pending authorization for a second Stable run after test-only repair
- Browser validation analytics: Pass
