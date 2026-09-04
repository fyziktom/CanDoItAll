# SB03 — Agent catalog controlled boundary

**Status:** Blocked by SB02  
**Outcome:** `AgentCatalogPanel` is a controlled service-free view; page-owned typed
intents replace child-owned load, dialogs, mutations, chat, and echo suppression.

## Owned requirements

R-022, R-024, R-031–R-033, R-040–R-042, R-045, related testability requirements.

## Prerequisites and reopen triggers

Checkpoint A accepted. Reopen if catalog state/intent ownership duplicates or page URL
behavior changes.

## Work

1. Add `AgentCatalogSnapshot`, `AgentCatalogViewState`, and prepared intent cases.
2. Add `IAgentCatalogController`/implementation/DI for repair, load/reload, privacy
   projection, update members, and delete team.
3. Refactor `AgentCatalogPanel` to receive state and emit intents. Retain only search,
   expanded tree nodes, and visual state locally.
4. Remove all feature `[Inject]` dependencies and all dialog/chat/mutation/load logic from
   the component.
5. Make `AgentsHomePage` handle selection, agent/team details, team members/delete,
   managed chat, notifications, controller reload, and result-driven state updates.
6. Preserve deep-link requested-agent open exactly once, with suppression owned by page
   transient/overlay state rather than child private fields.
7. Rewrite catalog tests around state/intents and page composition. No reflection.

## C# Architecture Impact

Creates the first fully controlled feature component and page/controller host seam.

## Boundary Ownership

Page owns selection and host actions; controller owns data/mutations; component owns
presentation-only state.

## Dependency Direction

Component -> state/intents only. Page -> controller/host. Controller -> feature services.

## Pattern Decision

PSR-03. Do not add a wrapper component.

## Testability Contract

Catalog component renders without feature DI. Controller tested directly. Requested-open
and mutation-result behavior tested through public page/component APIs.

## Partial Class Policy

Modify existing files only; no additional partial.

## Architecture Proof Required

- zero `[Inject]` properties on catalog component;
- old dialog/chat/load/mutation calls absent;
- direct controller tests;
- one-for-one replacement of two private-shape tests;
- Checkpoint B approval.

## Non-goals

No editor internals, team canonical route, provider panel, CSS redesign, or project move.

## Progression gate

Checkpoint B passes; catalog and page focused tests green; current URL behavior unchanged.
