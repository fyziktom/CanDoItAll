# CanDoItAll Agents UI Component Seams Bundle

**Reference ID:** `CDA-UI-SEAMS-AGENTS-01-v1`  
**Bundle kind:** Executable implementation bundle  
**Status:** Prepared  
**Prepared:** 2026-09-04  
**Repository:** `fyziktom/CanDoItAll`  
**Primary branch:** `components-decoupling`  
**Observed branch head:** `c225bf2445835bf12fa5054bc15571d2ce23b4fe`  
**Observed development base:** `d446dc2bad461c7e753cceb53a7969d6ff6b9cb2`  
**Recommended executor:** Codex GPT-5.6, high or xhigh reasoning  
**Remote writes:** Not authorized by this bundle

## Shared UI Component Seam Architecture Base

This bundle is governed by:

- Reference ID: `CDA-UI-SEAMS-BASE-v1`
- Expected repository path:
  `codex/bundles/UI_Component_Seams_Shared_Architecture_Bundle`
- Base kind: non-executable shared architecture reference
- Base version: `1`

The base does not supply this bundle's source scope, implementation steps, test commands,
or proof. This child bundle owns all of them.

### Applicable base rules

- [ ] Preserve component location during logical seam extraction unless relocation is an
      explicit outcome.
- [ ] Keep `AppComponents` independent from concrete feature modules.
- [ ] Classify state and move route-significant ownership to the page/workspace.
- [ ] Use the smallest real abstraction; avoid wrapper/interface inflation.
- [ ] Remove hidden service location and direct persistence access from Razor.
- [ ] Do not add partial files as the final architecture.
- [ ] Remove or rewrite incidental source-shape tests in the touched area.
- [ ] Record route, sandbox, and project-extraction readiness.

### Deviations

No deviation is approved at preparation time. Execution must stop and request an owner
or architecture decision before departing from the shared base.

## Objective

Refactor the first coherent Agents UI cluster in place so that:

1. `AgentsHomePage` owns typed route-significant workspace and overlay state instead of
   spreading it across strings, scalar fields, and child-private echo suppression;
2. dashboard/overview aggregation and direct EF access leave the Razor page;
3. `AgentCatalogPanel` becomes a controlled component driven by explicit state and typed
   intents rather than loading data, opening dialogs, mutating teams, and launching chats;
4. `AgentDetailsDialog` receives a stable typed section identity and performs external
   load/save/delete/reference-data work through one coherent editor controller;
5. useful behavior tests remain, while tests coupled to private fields, private methods,
   numeric tab indexes, or uninitialized concrete services are rewritten through public
   seams;
6. current URLs, visible behavior, data semantics, and sibling source-development mode
   remain unchanged.

This is a logical seam extraction. It deliberately does not create a new UI project or
sandbox host. The resulting boundaries must make those later steps materially easier.

## Primary source cluster

- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceTabs.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceRouteState.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkUiServiceCollectionExtensions.cs`
- new Agents-specific contracts, immutable state, pure policies, and controller/query
  implementations inside the existing `CanDoItAll.Modules.AgentFramework` project

The exact test inventory is in
[`inventories/02-test-impact-and-classification.md`](inventories/02-test-impact-and-classification.md).

## Target boundary at bundle closure

```text
existing URL/query compatibility
        |
        v
AgentsHomePage
  owns AgentWorkspaceSection + AgentsWorkspaceState
  owns selected agent/team and active agent-details request
  owns navigation and route-significant dialog/overlay requests
        |
        +--> IAgentsOverviewQuery
        |      aggregates overview, usage, HR-agent presentation,
        |      avatar lookup, and bound-resource count
        |
        +--> IAgentCatalogController
        |      loads/repairs/reloads the catalog and performs team mutations
        |
        +--> AgentCatalogPanel
        |      receives AgentCatalogViewState
        |      emits AgentCatalogIntent
        |      keeps only search/tree-expansion presentation state
        |
        +--> DialogService / NotificationService / IAgentChatLauncher
               host-level actions remain at the page boundary

AgentDetailsDialog
  receives AgentDetailsSection + optional AgentEditorSession
  emits stable section changes and save/delete results
        |
        +--> IAgentEditorController
               owns external load/save/delete/reference-data/capability workflows
        |
        +--> DialogService / NotificationService
               top-level editor presentation only
```

## Hard boundaries

1. Do not add canonical routes, route segments, Push/Replace navigation, compatibility
   redirects, or new query keys.
2. Preserve `/agents` and all current query-string behavior, including the existing
   simple-chat and usage-scope round trips.
3. Do not move existing components or create `CanDoItAll.Modules.AgentFramework.UI`.
4. Do not move feature components into `CanDoItAll.AppComponents` and do not add any
   feature-module reference to `AppComponents`.
5. Do not refactor `AgentProviderProfilesPanel`, request-history internals, workflows,
   voice, governance, diagnostics, Simple Chats, or other tabs except for call-site
   adaptation required by the new page-owned state.
6. Do not change `CanDoItAll.Components` or `CanDoItAll.FileTools`; both remain live
   sibling source dependencies.
7. Do not change the development Manager, Tailwind watcher, or `dotnet watch` commands.
8. Do not redesign the Agents UI, reorder visible sections, rename labels, or perform a
   general CSS cleanup.
9. Do not add another partial file for any target component/page.
10. Do not replace several visible dependencies with an `IServiceProvider`, service bag,
    or forwarding facade.
11. Do not create one interface per underlying service. The three planned seams are the
    overview query, catalog controller, and editor controller. Any fourth production
    interface requires a written pattern-decision addendum and architecture approval.
12. Do not retain or add product tests that inspect private fields/methods, count files,
    require an exact private source location, or construct uninitialized concrete services.
13. Comments added to source code and scripts must be in English.
14. Do not push, merge, publish, or alter protected remote branches without explicit user
    authorization.

## Execution order

| Order | Subbundle | Outcome | Dependency gate |
|---:|---|---|---|
| 1 | `01-freeze-baseline-and-characterize` | Refresh source/test evidence and freeze exact discovery counts | Shared base and branch are current |
| 2 | `02-agents-workspace-state-and-overview-query` | Typed page state and overview query; no direct EF in Razor | SB01 accepted |
| 3 | `03-agent-catalog-controlled-boundary` | Controlled catalog state/intents; page owns host actions | SB02 accepted |
| 4 | `04-agent-details-section-and-session-boundary` | Stable editor-section identity and explicit load/session seam | SB03 accepted |
| 5 | `05-agent-editor-command-boundary` | Save/delete/reference/capability I/O leaves dialog | SB04 accepted |
| 6 | `06-rewrite-tests-and-remove-shape-coupling` | Public-seam tests replace reflection/uninitialized-service harnesses | SB02–SB05 accepted |
| 7 | `07-integration-architecture-and-browser-closure` | Focused, broad, portability, architecture, and host proof | SB06 accepted |

Subbundles are restartable but not parallel-safe. SB02–SB06 intentionally overlap the
same Razor and test files and must execute sequentially.

## Observable behavior that must remain

- obsolete `tab=scenarios` falls back to Overview;
- current tab/query compatibility remains byte-for-byte equivalent for recognized state;
- current Simple Chats nested view and usage-scope state round-trip unchanged;
- HR Agent header action remains available and launches only the exact managed identity;
- “Load default agents and providers” retains its confirmation and busy behavior;
- selecting agents/teams continues to update page context;
- an `agentId` deep link opens the requested details exactly once without a child-private
  echo-suppression field;
- create/edit/delete agent and team flows preserve results and selection semantics;
- agent-details Identity, Runtime, Memory, Images, Project Structure Access, Workspace
  Tools, Secrets, Process Access, Capabilities, and Voice sections retain their order,
  labels, form behavior, and test IDs;
- provider, secret, and project loading retains current lazy/partial-failure semantics;
- save normalization, managed-agent deletion protection, confirmation behavior,
  capability assignment/verification, external-root migration, storage selection,
  thinking-effort validation, auto-approval warning, and avatar generation remain intact.

## Definition of done

The bundle is complete only when all of the following are true:

- `AgentsHomePage` has no direct `IDbContextFactory<AppDbContext>` dependency and no EF
  query logic;
- route-significant Agents section, selected agent/team, usage selection, Simple Chats
  state, and active agent-details target have one typed page/workspace owner;
- current URL behavior is unchanged and all ten existing route-state unit cases pass;
- `AgentCatalogPanel` has no feature-service injection and does not open dialogs, launch
  chats, repair/load catalogs, or perform team mutations;
- catalog search and tree expansion remain local presentation state;
- `AgentDetailsDialog` no longer injects workspace/provider administration, Projects,
  Secrets, direct infrastructure factories, or persistence services;
- `AgentDetailsDialog` uses a stable `AgentDetailsSection` identity rather than exposing
  numeric tab indexes as semantic state;
- editor load/save/delete/reference-data/capability workflows are owned by one coherent
  controller and are directly testable;
- no new partial file, project, project reference, generic lifecycle base, service bag,
  or duplicate DTO family is introduced;
- the 46 pre-existing focused component cases are preserved or replaced one-for-one as
  behavior coverage, with any intentional count change explained before execution;
- target test files contain no reflection into private component state and no
  `RuntimeHelpers.GetUninitializedObject` workaround;
- direct controller/state tests and durable forbidden-dependency tests pass;
- the affected production project, focused Unit and Components slices, final stable gate,
  portability-static gate, and large-desktop browser smoke pass;
- final review records `route-ready`, `sandbox-ready`, and `project-extraction-ready`
  decisions plus remaining coupling;
- no shared-base deviation remains unapproved.

## Baseline policy

The recorded SHAs are discovery evidence, not immutable pins. Before editing, fetch the
current `components-decoupling` and `development` heads. If the target branch is behind,
contains additional functional changes, or no longer contains `CDA-UI-SEAMS-BASE-v1`,
stop and reconcile the bundle before implementation.

## Bundle map

- [`README.cs.md`](README.cs.md) — Czech owner summary
- [`prompt.md`](prompt.md) — primary execution prompt
- [`bundle.json`](bundle.json) — machine-readable bundle identity
- [`inputs/`](inputs/) — owner directive, shared-base reference, and source baseline
- [`analysis/`](analysis/) — current coupling, test debt, scope rationale, and routing context
- [`architecture/`](architecture/) — required C# inventory, target boundaries, decisions,
  testability, state contract, and component assessments
- [`requirements/`](requirements/) — normalized requirements and invariants
- [`inventories/`](inventories/) — source, dependency, test, and target-type inventories
- [`plan/`](plan/) — phase, architecture checkpoints, invalidation, and proof plan
- [`traceability/`](traceability/) — requirement and input closure
- [`commands/`](commands/) — exact validation command catalog
- [`shared-prompts/`](shared-prompts/) — implementation and independent-review prompts
- [`subbundles/`](subbundles/) — seven executable work units
- [`reviews/`](reviews/) — readiness, architecture gate, execution report, and closure
- [`proof/README.md`](proof/README.md) — evidence placement contract
