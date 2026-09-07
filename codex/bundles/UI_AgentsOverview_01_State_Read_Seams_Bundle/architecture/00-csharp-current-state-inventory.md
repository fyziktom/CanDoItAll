# Current ownership and adjudication

[Source receipts](../inventory/source-inventory.json) identify preparation bytes. Re-read exact symbols on re-entry; line numbers are not future pins.

| Current source | Responsibilities/evidence | Treatment |
|---|---|---|
| `Pages/AgentsHomePage.razor.cs` (850 lines; normal Razor partial) | Six injected collaborators: navigation, workspace query, chat launcher, warmup, notifications, dialogs. Route/header, copied counts, snapshots, chart mapping, flags and callbacks. | Remove aggregate/read/mapping ownership; retain shell/header and thin typed effect dispatch. |
| `Pages/AgentsHomePage.razor` / `.razor.css` | Persistent header/HR/defaults/tabs; Overview stats/charts/rankings/teams. Header and Overview styles share file. | Keep chrome; isolate actual Overview subtree/CSS without copies or redesign. |
| `Services/AgentsWorkspaceQuery.cs` | Three constructor collaborators: workspace, usage query, bound-resource query. Scoped seam. Shell joins overview/usage/HR/bound count; HR failure partial, others abort envelope. Usage read already avoids shell refresh. | Reuse cohesive interface; split demand operations. |
| Workspace state, route and section policies | One typed workspace state. Only Providers/RequestHistory are history hosts. Overview scope options Agents/Chats/Both. | Preserve keys and policy; no enum-ordinal routing or generic Relay/All exposure. |
| Models `Workspace/AgentOverviewModels.cs` | Totals, team shortcuts, older usage value projections. | Reuse values; no second header totals store or wholesale Models move. |
| Usage contracts/query | Scope-stamped snapshot, typed source states, good partial data, unpriced/unknown counters, metadata/deduplication validation. | Preserve algorithm; session owns cancellation/acceptance. |
| `ProviderUsageConsumerList.razor` | Actual current child at two page sites; pure rows/avatar/loading/rank. | Preserve real list and independent input collections; move with Surface in O03. |
| `AgentOverviewUsageList.razor` | Older pure list still tested; not used by current page. | Inventory only, not automatic extraction/deletion. |
| `AgentUsageDisplay.cs` | Pure formatting, used by current/old lists and all three dialogs. | One helper owner; O03 may move actual helper with access/namespace compatibility and all consumers rebuilt. No copied helper or wrapper back to Module. |
| `AgentUsageDialog`, `ProviderUsageDialog`, `ModelUsageDialog` | Lazy query in OnInitializedAsync, chart/table rendering, no read token/disposal. Page passes requested scope. | O02 only owned load/disposal and accepted-scope opening; no wholesale internals refactor. |
| Catalog warmup | Five constructor collaborators; defaults invokes organization repair, mock catalog, CRM/HR projection and workflow seeding. | Keep confirmation/command with page. Overview retry never repeats command. |
| HR/team actions | Fixed HR identity/context readiness; team enters existing Agents route. | Preserve HR/global navigation; one typed team intent and current push semantics. |

Source-confirmed risks requiring meaningful O00 RED: every usage completion is accepted; old finally clears busy; route refresh is suppressed while usage is loading; no owned page read CTS/disposal/generation; dialog opening uses desired scope; raw infrastructure errors can surface. These are source adjudications, not executed Overview failures.

Source-confirmed coupling: LoadDashboard copies overview counts; ReadShell joins independently useful failure domains. The new regression fixture must exercise current page events before extracted types exist. Missing-type compilation is not semantic RED.

Preserve established history lazy reads, no-shell usage refresh, partial HR, route round-trips/defaults, confirmed Defaults, persistent HR, provider history independence and catalog first-save echo. Passing behavior is characterization, not a defect to repair.

## Fresh CodeAnalytics

`[snap-20260907070141-564e9b6c]` was built after Capabilities-03 closure: scoped Module, 205 documents, 541 types, 5697 members, 55 service registrations, 216 findings, 20 questions, 23 diagnostics, no blocking error. Relevant page findings: 850 lines and 105 members. Use these to locate ownership, never as count gates.

Existing reported cycles: Module/Hosting namespaces; ImageGenerationAgentRuntimeToolProvider/nested ImageGenerationToolBuilder types. Neither is introduced/repaired here. Empty parsed project references and omitted Razor-generated bodies limit coverage. The snapshot does not prove whole-solution acyclicity; evaluated MSBuild/direct Razor are required. See [compact receipt](../inventory/codeanalytics.json).

Composition registers the query in AgentFrameworkUiServiceCollectionExtensions. Page/dialogs construct chart models directly. No Overview session/controller exists; no new global service is justified. Tests and missing witnesses are specified in [testability](04-csharp-testability-plan.md).
