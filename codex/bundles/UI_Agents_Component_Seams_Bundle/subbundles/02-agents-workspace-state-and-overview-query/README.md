# SB02 — Agents workspace state and overview query

**Status:** Blocked by SB01  
**Outcome:** `AgentsHomePage` owns typed semantic state and consumes one overview query;
direct EF and multi-source dashboard orchestration leave Razor without URL change.

## Owned requirements

R-020–R-022, R-024, R-030, R-036, R-040–R-041, R-045, relevant proof requirements.

## Prerequisites and reopen triggers

SB01 accepted. Reopen if route-state or overview contracts move later.

## Work

1. Add `AgentWorkspaceSection`, `AgentsWorkspaceState`, `AgentDetailsRequest`, and pure
   current-key mappings.
2. Keep `AgentWorkspaceTabs` and `AgentWorkspaceRouteState` as current URL compatibility
   surfaces. Preserve all recognized URL output exactly.
3. Replace page string/scalar semantic ownership with the typed state. Keep loading and
   dashboard data outside navigation state.
4. Add `IAgentsOverviewQuery`, implementation, request/result types, and DI registration.
5. Move overview/usage/HR-agent/avatar/bound-resource aggregation and direct EF access out
   of `AgentsHomePage`.
6. Preserve partial usage warning, missing HR agent, loading, and retry behavior.
7. Add direct state/query tests and adapt page tests to a fake overview query.
8. Do not introduce catalog/controller behavior yet beyond state fields needed by SB03.

## C# Architecture Impact

Moves one cohesive read workflow and establishes page-owned semantic state.

## Boundary Ownership

Page: route-significant state and presentation. Query: external read aggregation.

## Dependency Direction

Razor -> `IAgentsOverviewQuery`; implementation -> Workspace/usage/EF. No EF -> Razor.

## Pattern Decision

PSR-01 and PSR-02. No metric-specific interfaces.

## Testability Contract

State/query direct tests plus existing route/home behavior. Use exact discovery from
SB01/SB02 proof.

## Partial Class Policy

Modify existing `.razor.cs`; add no new page partial.

## Architecture Proof Required

- page no longer references EF/AiResourceBinding;
- query direct tests and DI resolution;
- URL round-trip unchanged;
- before/after page dependencies;
- Checkpoint A approval.

## Non-goals

No new routes, catalog rewrite, details editor rewrite, visual changes, broad gate.

## Progression gate

Checkpoint A passes and focused state/query/home/route tests are green.
