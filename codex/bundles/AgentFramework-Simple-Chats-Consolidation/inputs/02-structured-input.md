# Structured input

## Core Objective

- Consolidate Simple Chats into the AgentFramework product experience while keeping the implementation in cohesive reusable MAF libraries and combining Agent/Simple Chat cost analytics through typed source-neutral contracts.

## Success Criteria

- Simple Chats is immediately next to Agents on /agents.
- Provider setup remains canonical in Providers.
- Core, Application, Runtime, Persistence, and Components live under src/MAF/SimpleChats.
- Agent and Simple Chat usage supports Agents, Simple Chats, and Both without double counting or false historical cost.
- /chats remains redirect compatibility; APIs/tables/scopes stay stable.
- Main/floating Agent and Simple Chat E2E behavior passes.

## Hard Constraints

- Do not dump feature internals into Modules.AgentFramework.
- Do not duplicate existing generic AgentFramework.Llm.* or Conversations.* foundations.
- Do not dual-write EF usage into the Agent file store.
- Do not query-time reprice historical usage.
- Do not infer Simple Chat from ChatSessionId or BasicChat.
- Do not implement during preparation.

## Allowed Side Effects

- During later execution only: add/move projects and namespaces, add one append-only usage/pricing migration, update composition/API/test references, consolidate routes/navigation/UI, and remove old projects.

## Source Artifacts

- See inputs/01-source-artifacts.md.

## Input Coverage Signals

- Logical provider/Agent connection.
- Three-way cost scope.
- Library isolation rather than module merge.
- Simple Chats tab placement.
- MAF namespace/project grouping.
- Detailed bundle-only preparation.

## Dependency And Sequencing Signals

- Usage contracts precede producer adapters and dashboard.
- Core/Application precede Runtime/Persistence/Components.
- Persistence pricing evidence precedes unified analytics.
- Components and analytics converge at Agent page integration.
- Old projects are deleted only after every caller migrates.

## Validation Expectations

- Focused non-zero tests per subbundle.
- Governed architecture/data/composition proof.
- Behavioral reusable component proof.
- One Stable gate at the frozen final candidate.
- Named Playwright plus Playwright MCP main/floating/cost-scope proof.

## Evidence Contract

- Per-subbundle manifest/report/invariants/transcripts/hashes as declared.
- CodeAnalytics dependency/cycle proof at checkpoints.
- EF migration/model/transfer proof.
- Browser normal/open-overlay screenshots at 1600x1000.
- Final user-verification handoff.

## UI Validation Strategy

- Application UI targets large-screen desktop 1600x1000.
- Primary surfaces: Agent dashboard and Simple Chat workspace.
- Supporting content: compact page/inner tabs, scope selector, charts, lists, dialogs, floating windows.
- Stats: scoped usage stats support dashboard; configured catalog stats remain unfiltered.
- Editor: definition editing remains a Wide dense-chrome dialog with Extended/explicit prompt rows.
- First viewport and scroll owners are specified in SB07-SB09/SB11.
- Reusable BaseLib itself is not changed, so small/medium BaseLib proof is not in scope.

## Browser Validation Analytics

- Each UI subbundle records route, viewport, Playwright MCP actions/assertions, normal/open-overlay screenshots, first viewport, scroll owner, container-aware controls, console/page errors, and result.

## Working Assumptions

- Existing API/schema compatibility is required.
- Existing generic LLM libraries remain viable foundations.
- A neutral composite read model is sufficient; central transactional ledger is not required.
- Historical pricing provenance is unknowable when not stored.

## Primary Risks

- destructive EF migration from CLR relocation;
- weakened profile/lease fencing;
- duplicate attempt cost;
- fake zero legacy cost;
- project cycle or permanent facade;
- Razor/DI/shell duplication;
- Agent floating regression.

