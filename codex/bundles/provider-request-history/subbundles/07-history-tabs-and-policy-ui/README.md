# SB07 - History tabs and global policy UI

## Status

- Implementation: `Not started`
- Automated verification: `Not started`
- Browser verification: `Not started`
- Architecture acceptance: `Not started`
- This README prepares the phase only. No product code, runtime setting, test, provider request, or migration is executed during bundle preparation.

## Objective

Expose the SB06 authorized history service through two scopes of one reusable UI: a History tab immediately after provider Sharing, and a Request history tab on Agents over authorized providers in the current instance/database profile/security partition. Add a separate, authorized global policy editor to the existing Settings surface.

The user must explicitly request history and content. The UI must not turn route entry, tab activation, filter editing, background rendering, or opening a control into an unbounded read. Policy loading/applying is a separate operation from searching history.

## Success Criteria

- Both history hosts render a useful not-requested state and execute zero history, aggregate-usage, totals, facet, canonical-content, or external provider-operation reads before Search. Bounded local catalog and permission metadata needed by the existing shell is permitted; historical lookup is not.
- One bounded server page is displayed with typed time/pricing/identity evidence and explicit applied filters. Cursor paging never widens scope or claims an immutable snapshot.
- History inputs and Enter-key handling cannot submit provider edits; provider Save validation remains intact.
- Content requires explicit disclosure and current authorization; policy read/apply requires its separate management privilege.
- Existing provider, Agents, and Settings behavior passes focused component regressions and 1920x1080 normal/overlay proof.

## Covered Inputs

- Own the provider and all-provider search surfaces (N003–N005), and present the caller, price, canonical-detail and global-policy outcomes mapped below without expanding the preparation-only scope.

Use [structured inputs](../../inputs/02-structured-input.md) and the final normalized requirements/traceability mapping.

| Input | SB07 ownership |
|---|---|
| N003, N005 | The two required tabs, one shared feature, correct provider scope and current-instance semantics. |
| N004 | Explicit Search, finite selected range/filters, no initial history/count/facet reads. |
| N001, N002 | Display honest price provenance and managed credential ID separately from subject; do not calculate pricing or manufacture client identity in Razor. |
| N006, N007, N010 | Display canonical owner links and bounded explicit details without loading/copying whole conversations. |
| N008, N009 | Authorized global Light/Detailed policy and retention/quota controls; defaults and persistence come from SB03/SB06. |
| N011 | Real form/controller/component boundaries, small page edits, no reverse module dependency or growing partial. |
| N012 | Preparation only now; implementation and all runtime acceptance remain Not started. |

## Prerequisites

- Enter only after SB03 policy/storage and SB06 authorized query/detail/policy gates pass; retain their current contracts and resolve supported component/browser tooling before the corresponding implementation or runtime proof.

1. **SB03 passed:** additive history/policy storage, typed versioned policy, immutable entry identity/time basis, expiry/quota semantics, and the relevant migration/rollback proof are available.
2. **SB06 passed:** metadata/detail/content/policy operations enforce trusted active profile/security scope, protected cursor bindings, explicit privileged access, bounded server predicates, and authorization/profile recheck before publishing a response. Component visibility is not that security boundary.
3. The approved contracts in [target solution](../../architecture/01-target-solution.md), [lifecycle](../../architecture/05-history-data-lifecycle.md), [search/security](../../architecture/09-search-security-contract.md), [pricing/capture](../../architecture/10-pricing-and-capture-contract.md), and [UI analysis](../../architecture/08-ui-search-analysis.md) are consistent. Use the final SB01-approved type names; names marked proposed here do not claim an existing API.
4. Deterministic scoped query/detail/policy fixtures are available. They cover two providers, shared/imported profiles, managed credentials with the same subject, legacy time basis, incomplete index coverage, all relevant pricing states, unavailable/expired content, and version conflicts without invoking a paid model.
5. Read the component skill and compact UI reference before implementation. Revalidate the shortlisted component parameters through Component MCP before markup changes. Earlier successful selection/usage calls are recorded in the UI analysis; a later Settings recommendation attempt returned `Transport closed`. This is an execution prerequisite, not permission to invent a wrapper or restart a service.
6. Browser setup must work before runtime closure. The preparation attempt failed before page access with the recorded Windows sandbox ACL error. No deployed screenshot or successful runtime reproduction exists from this preparation.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceTabs.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceRouteState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectManagerSummaryPanel.razor`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/DataVisualization/DataGrid.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/Forms/Numeric.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

Linked source context:

All references below were read as source; they are not claims that future history code already exists.

| Source | Why it matters |
|---|---|
| [AgentProviderProfilesPanel.razor:67](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor:67) | Current provider `EditForm` wraps every tab; Sharing is at line 236. |
| [AgentProviderProfilesPanel.razor.cs:138](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs:138) | Provider editor replacement on stable provider selection. |
| [SharedProviderManagementPanel.razor.cs:32](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor.cs:32) | Existing Sharing loads on parameter changes; do not copy that lifecycle into History. |
| [AgentsHomePage.razor:80](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor:80) | Existing secondary-tab host and component branches. |
| [AgentsHomePage.razor.cs:334](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs:334) | First-render dashboard load; line 375 currently invokes aggregate usage regardless of the selected tab. |
| [AgentWorkspaceTabs.cs:3](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceTabs.cs:3) and [AgentWorkspaceRouteState.cs:28](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceRouteState.cs:28) | Stable tab constants, allowlist, and route parsing. |
| [ProjectManagerSummaryPanel.razor:21](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectManagerSummaryPanel.razor:21) | Existing explicit-load promise and button at line 87; cancellation/state ownership at lines 468, 584, and 595. |
| [DataGrid.razor:161](C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/DataVisualization/DataGrid.razor:161) | Materializes supplied data; line 224 implements in-memory paging, not a server query. |
| [SettingsPage.razor:29](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor:29) | Existing Settings tab host; feature panels are sibling branches outside the workspace-default Save form. |
| [SettingsPage.razor.cs:30](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs:30) | Settings tab list; allowlist at line 173 and legacy provider redirect at line 138. |
| [ApiTokenAdministrationPanel.razor:25](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ApiTokenAdministrationPanel.razor:25) | Existing typed `InputNumber` pattern; permission check at line 78 precedes token-list disclosure. |
| [Numeric.razor:54](C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/Forms/Numeric.razor:54) | Existing shared Numeric silently preserves/clamps values at lines 54-76; that behavior is unsuitable for explicit policy-validation errors. |
| [CanDoItAll.Modules.Workspace.csproj:20](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj:20) and [CanDoItAll.Modules.AgentFramework.csproj:59](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj:59) | Workspace owns Settings; AgentFramework already depends on Workspace, so reversing that edge would introduce a cycle. |
| [WebApiTokenAdministrationAccess.cs:9](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/WebApiTokenAdministrationAccess.cs:9) | Existing trusted interactive/HTTP principal adapter precedent, not a reason to expose host types to UI contracts. |
| [CanDoItAll.Tests.Components.csproj:4](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj:4) | Existing .NET 10, bUnit, xUnit, and VSTest-based component test project. |

## UI Composition Contract

- **Primary surface:** a request table after explicit Search; a not-requested state before Search. Compact range/provider/model/client/workload/outcome/pricing filters stay above it.
- **Supporting content:** current applied range, query/freshness time, typed time basis, coverage warnings, safe caller attribution, and page-local row count. No automatic total, summary, chart, or historical-facet lookup.
- **Two hosts:** `ProviderRequestHistoryPanel` receives typed `SingleProvider` or `AllAuthorized` scope. The latter covers local/imported profiles in the current instance/profile/security partition only; it is not remote log federation.
- **List/editor organization:** details open in a controlled read-only dialog, not a transcript editor permanently below the table. Global policy has its own Settings tab and own form; it is not embedded in provider Save or history Search.
- **Shared controls:** existing `Tabs`/`TabsItem`, `SecondaryTabs`, `FormStack`, `FormRow`, `FormField`, `Stack`, wrapping `Cluster`, typed `DropDown`, `TextBox`, `Button`, `DataGrid<TItem>` with typed templates, `StatusBadge`, `Alert`, `EmptyState`, `LoadingState`, and `Dialog`. Use existing typed `InputDate`/`InputNumber` under `FormField` where the shared library does not provide the required explicit validation semantics.
- **Paging:** `DataGrid.AllowPaging=false`, only one bounded server page supplied, shared Previous/Next buttons using protected cursors. No full-history list followed by client `Skip/Take`.
- **Text/dialog sizing:** a `ModalSize.Wide`, dense-chrome detail dialog permits metadata and bounded read-only content; `TextAreaSize.Standard`/`Extended` follows the actual bounded segment. A short policy-change confirmation uses `ModalSize.Compact`. No giant or hidden prompt body in a row.
- **First viewport and scrolling:** at 1920x1080, show the existing page identity/tab strip, compact filters, Search, and useful empty/result state. Preserve the shell's intentional scroll owner; the details dialog body scrolls while its title/footer remain usable. Validate the provider pane beside its real 25rem provider list, not only the wider all-provider view.
- No new package, raw structural Tailwind, one-off page CSS, mobile redesign, or BaseLib change is planned. Reusable BaseLib changes require separate scope review and small/medium/large proof.

## Deliverables

- Deliver one reusable explicit-search feature in both hosts, a separate provider mutation form, a Workspace-owned authorized policy editor, and the focused behavior/browser evidence needed by SB08.

1. **Shared history feature in AgentFramework UI.** Proposed owners: `ProviderRequestHistoryPanel.razor`, `ProviderRequestHistorySearchController.cs`, and `ProviderRequestHistoryDetailsDialog.razor` under the existing module's Pages/Components area. The page composes these; it does not own the state machine.
2. **Concrete provider form boundary.** Hoist `Tabs` outside the provider mutation form. Extract `ProviderProfileEditorForm.razor` for editable pane content, one stable parent-owned provider EditContext, existing validation, and shared save/footer callbacks. Server tab rendering mounts only the active editable form. Sharing/History are sibling panes outside it. History owns its own search form/context. No nested form and no guard that silently swallows accidental Save.
3. **Agents route integration.** Add one stable Request history tab constant/allowlist entry and one component branch. Separate existing overview usage loading from cheap shell/catalog metadata so entering Providers/History or the dedicated history route does not automatically read the aggregate usage history.
4. **Typed query presentation.** Defaults come from central options: last-24-hours draft, 50 rows, max 200/page and 31-day selected interval. Display stable EntryId, SortAtUtc with TimeBasis, managed credential/subject separately, and explicit pricing evidence including ProviderReported. No fabricated start time, current-catalog repricing, currency merging, or null-as-free.
5. **Search lifecycle.** Draft/applied separation; one captured immutable request per Search; cancellation and generation ownership; scope/epoch/revision invalidation; cursor paging against the applied query; explicit error/coverage states; no automatic summary/facet reads.
6. **Authorized detail disclosure.** Details and content are separate explicit requests. Canonical content stays with its owner. History-owned detail is typed current-turn input/response, centrally bounded (default 32 KiB UTF-8 each, seven days), encoded, and visibly incomplete where applicable. Arbitrary relay transcripts must not use a last-user-message heuristic.
7. **Global policy panel in Workspace.** Proposed `ProviderHistoryPolicyPanel.razor` and a small policy controller, if required by state complexity, under `CanDoItAll.Modules.Workspace/Pages/Components`. Add one named Settings key such as `ProviderHistorySettingsTabKey`/`provider-history` to the existing tab list and allowlist. The panel uses only neutral history policy/access ports, never an AgentFramework UI reference.
8. **Explicit policy load/apply.** Settings entry can check permissions but does not read policy/history automatically. Load policy is explicit; Apply validates and uses the returned version. Preview/confirmation of shortening or purge is a separate authorized bounded action; no hidden age/count scan or implicit purge. Conflict preserves the draft and requires an explicit reload/review, not a force overwrite.
9. Focused existing regressions, new meaningful component/controller tests, recorded discovery, and desktop/overlay evidence. Update the bundle execution report only with actual results.

## Dependency Impact

- Direct prerequisites are SB03 policy/storage and SB06 authorized query/detail/policy operations. Their approved semantics are consumed, not redefined, by SB07.
- **SB08 depends on this phase.** Its source/consumer acceptance cannot pass with a hidden automatic query, unsafe form submission, stale cross-profile result, missing permission boundary, or absent policy-version/error UI.
- Scope, route, contract, auth revision, time-basis, price-state, policy-version, component-package, and form-boundary changes invalidate the relevant SB07 proof.
- If SB07 requires a new server query shape, source-content permission, retention behavior, or policy operation, reopen its owner phase before implementation. Do not work around it in Razor.
- Preserve Agents overview pricing/scope, existing provider edit/save, Sharing/connections lazy behavior, Settings provider redirect, and database profile UI.

## Validation Depth

- Proof tier: **Behavioral**.
- Owning automated project: `C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`.
- Supporting query/security proof: the exact SB06 gate remains current. UI tests do not replace server authorization and bounded SQL proof.
- Critical-foundation classification: not a storage/capture foundation; it is the required behavior and presentation gate for SB08.
- Broad-gate decision: **Not required in SB07** while changes stay within the listed UI/host composition boundaries. A shared BaseLib, common authorization, or broad route contract change requires an explicit reopen decision and one broader gate at the frozen SB08 checkpoint, not repeated whole-repository runs.
- Invalidation keys: provider/partition identity, database epoch, authorization revision, fixed-scope versus draft-filter semantics, protected cursor contract, stable EntryId/SortAtUtc/TimeBasis, typed pricing states, policy version/expiry semantics, provider EditContext ownership, and shared component version.
- Source-verified component baseline expectation: **14 existing Fact methods** selected exactly below. They have not been discovered or executed in this preparation.
- Separate Unit route regression: `CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkSimpleChatsRouteTests.UsageScopeHasTypedDeterministicParsing` in `CanDoItAll.Tests.Unit.csproj` is one existing Theory with **four InlineData cases**. Expect four expanded cases for its exact filter; record actual runner discovery and all four results separately from the 14 component Facts.
- New behavior expectation: **16 proposed Fact methods** listed under Testability Contract. Those names do not exist yet; record the actual implemented names and expected count before executing their gate.

### Existing focused regression cases

| ID | Existing fully qualified test | Source |
|---|---|---|
| E01 | CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.Toolbar_is_icon_only_and_connections_load_only_when_opened | [line 10](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ProviderAdministrationLayoutTests.cs:10) |
| E02 | CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.Publication_settings_do_not_render_or_load_source_connections | [line 33](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ProviderAdministrationLayoutTests.cs:33) |
| E03 | CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.Compact_filter_can_be_cleared | [line 46](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ProviderAdministrationLayoutTests.cs:46) |
| E04 | CanDoItAll.Tests.Components.AgentFramework.AgentProviderProfilesPanelPricingTests.Provider_editor_surfaces_model_prices_on_dedicated_prices_tab | [line 14](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/AgentProviderProfilesPanelPricingTests.cs:14) |
| E05 | CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests.Hr_agent_avatar_action_remains_in_the_page_header_across_module_tabs | [line 79](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/AgentsHomePageTests.cs:79) |
| E06 | CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests.Simple_chats_follows_agents_and_renders_both_nested_workspaces | [line 129](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/AgentsHomePageTests.cs:129) |
| E07 | CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests.Usage_scope_defaults_to_both_and_is_forwarded_to_detail_dialogs | [line 171](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/AgentsHomePageTests.cs:171) |
| E08 | CanDoItAll.Tests.Components.ProjectStructure.ProjectStructurePageDatabaseSwitchTests.Manager_summary_tab_query_selects_an_explicitly_lazy_report | [line 19](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageDatabaseSwitchTests.cs:19) |
| E09 | CanDoItAll.Tests.Components.ProjectStructure.ProjectStructurePageDatabaseSwitchTests.Manager_summary_snapshot_survives_server_rendered_tab_disposal | [line 40](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageDatabaseSwitchTests.cs:40) |
| E10 | CanDoItAll.Tests.Components.ProjectStructure.ProjectStructurePageDatabaseSwitchTests.Manager_summary_activity_dialog_is_created_only_after_explicit_open | [line 131](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageDatabaseSwitchTests.cs:131) |
| E11 | CanDoItAll.Tests.Components.Shell.SettingsPageDataSourcesTests.Legacy_provider_settings_url_redirects_to_authoritative_agents_tab | [line 15](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/SettingsPageDataSourcesTests.cs:15) |
| E12 | CanDoItAll.Tests.Components.Shell.SettingsPageDataSourcesTests.Settings_page_renders_data_sources_tab_with_saved_profiles_and_editor_actions | [line 37](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/SettingsPageDataSourcesTests.cs:37) |
| E13 | CanDoItAll.Tests.Components.ApiTokenAdministrationTests.TOKEN_ADMIN_list_is_lazy_and_revoke_delete_require_confirmation | [line 36](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ApiTokenAdministrationTests.cs:36) |
| E14 | CanDoItAll.Tests.Components.ApiTokenAdministrationTests.TOKEN_ADMIN_access_denial_prevents_data_loading_and_rechecks_every_action | [line 67](C:/repositories/CanDoItAll/tests/Components/CanDoItAll.Tests.Components/ApiTokenAdministrationTests.cs:67) |

The existing API-token tests are behavior precedents/regressions. They are not authorization to issue, reveal, revoke, or delete a real token in browser proof.

The separate mandatory Unit anchor is [AgentFrameworkSimpleChatsRouteTests.cs:86](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/AgentFrameworkSimpleChatsRouteTests.cs:86), fully qualified as `CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkSimpleChatsRouteTests.UsageScopeHasTypedDeterministicParsing`. Its `[Theory]` at line 81 has four InlineData inputs (`agents`, `simple-chats`, `both`, `invalid`) at lines 82-85. This protects the shared typed route parser; it is not one of the 14 component Facts or the 16 proposed component cases. Its project is [CanDoItAll.Tests.Unit.csproj:4](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj:4).

## Implementation Steps

1. Verify SB03/SB06 proof, final contract names, active branch/diff, source-size baseline, and component guidance. Record the exact planned UI files and test discovery selectors.
2. Add controller tests for draft/applied state, explicit loading, scoped paging, delayed completion, cancellation, and scope/authorization invalidation using recording ports and controllable completion sources.
3. Isolate the provider mutation form with ProviderProfileEditorForm. Preserve the shared provider EditContext and validate Save before mounting History beside Sharing.
4. Implement the shared history panel, query form, bounded table, and separate details dialog using the approved components. Keep all side effects behind explicit actions.
5. Wire the provider host and Agents route. Ensure route/host entry cannot execute the existing all-history usage query. Preserve overview behavior when Overview is explicitly selected.
6. Implement the Workspace-owned policy panel against neutral policy ports. Separate Load, field edits, Preview/confirmation, and versioned Apply. Expose typed errors without leaking provider/raw storage exceptions.
7. Add focused component regressions and proposed history/policy cases; verify server recheck and protected cursor behavior through the current SB06 integration proof.
8. Build the affected application, component-test and Unit-test projects, list the exact tests, compare actual discovery with each project's approved expectation, then run only those gates. Keep the four expanded Unit route cases separate from the 14 existing and 16 proposed component cases.
9. Run controlled 1920x1080 browser proof, review all screenshots and service-call counters, and record actual evidence. Do not accept a quiet browser network panel as proof of zero Blazor Server reads.
10. Perform architecture review, update this phase and the execution report with actual evidence, and release SB08 only after the progression gate passes.

## Scope Exceptions

- Exact deployed localhost unpriced reproduction is deferred to controlled runtime proof; this preparation did not access the page successfully.
- The policy editor does not change canonical chat/workflow retention. The default 30-day metadata policy applies to standalone/direct and relay history; retained canonical projections follow their source owners.
- History-owned detailed content is not full wire replay, and Light mode has no private text excerpt. Unsupported relay detail shapes remain explicit metadata-only records.
- No remote history federation, arbitrary full-text prompt search, export, billing/chargeback, or exact IDM/EGCP person mapping.
- No mobile product work or BaseLib redesign. If a shared component actually prevents correct composition, reopen scope and require its additional proof.

## Do Not Do

- Do not fetch history, totals, summary counts, historical facets, or canonical content in initialization, parameter setters, tab selection, control opening, route hydration, or incidental renders.
- Do not place History inside provider Save, nest EditForms, or rely only on non-submit buttons to avoid Enter-key implicit submission.
- Do not pass IQueryable, EF entities, provider configuration, bearer values, or transcript JSON into UI state.
- Do not fetch all rows then page, build an unbounded result cache, or use mutable provider/model display labels as identity.
- Do not treat a draft filter change as a new applied query or widen a host-fixed provider scope.
- Do not infer permission from a row GUID, subject equality, a context header, a cursor, UI visibility, or authorization being disabled.
- Do not add Workspace -> AgentFramework/ProviderManagement/UI references. The Settings policy panel consumes neutral ports.
- Do not silently clamp policy input, overwrite a policy version conflict, purge on an ordinary field edit, or recalculate historical prices from today's catalog.
- Do not make a new partial of a large page or add the whole feature to AgentsHomePage.razor.cs.

## Acceptance Checklist

- [ ] Provider History immediately follows Sharing; the dedicated Agents Request history tab uses the same panel.
- [ ] Current instance/profile/security scope is visible; a provider host is fixed to its saved local provider ID; unsaved providers cannot search.
- [ ] Mount, tab switch, filter edit, and control open produce zero history/aggregate/totals/facet/content/external-provider calls; permitted bounded local catalog reads do not scan history.
- [ ] One valid Search captures immutable applied filters; invalid range/page/identifier input shows field errors and no query.
- [ ] Paging uses only the applied query/cursor; draft changes are labeled; host scope/profile/auth changes cancel and clear.
- [ ] EntryId and SortAtUtc/TimeBasis remain stable display/query facts; legacy evidence is not presented as an invented dispatch time.
- [ ] Live paging does not promise a snapshot or insertion fence. Late-backfill discovery uses explicit Search/Refresh without duplicate stable IDs.
- [ ] Loading, cancel, no results, failure with labeled prior results, denied access, and coverage gaps are distinct.
- [ ] Search/Enter/Next/Details do not save provider edits; provider Save still validates and persists the intended model.
- [ ] Details and content have separate explicit requests; owner/current permission is rechecked; unavailable/expired/unsupported detail is honest.
- [ ] Price display distinguishes ProviderReported, calculated/estimated/free/unpriced/legacy evidence and currency; no null-as-zero.
- [ ] Global policy requires explicit authorized Load and versioned Apply; draft edits trigger neither history queries nor writes.
- [ ] Retention shortening/purge requires its explicit authorized preview/confirmation; mode changes do not silently delete or extend prior detail.
- [ ] Permission/profile changes during a server/UI await cannot publish stale metadata, content, or policy results.
- [ ] Workspace depends only on neutral history contracts for this feature; no reverse module edge or new shared-library edit.
- [ ] Expected versus actual test discovery is recorded, all selected tests pass, and screenshots/counters support the stated behavior.
- [ ] No paid provider call, real token lifecycle mutation, or unrelated data operation was used without execution-specific authorization.

## Proof Required

- Record actual focused build/discovery/test results, service-call and race assertions, architecture checks, and reviewed desktop/overlay artifacts before this phase can pass. All execution remains future work.

Evidence is future work under `proof/SB07/` at the bundle root; all relative `browser/` artifact paths below resolve beneath that directory. Do not create empty success artifacts during preparation.

1. **Build evidence:** normal affected Web/module, component-test and focused Unit-test project build after edits, with actual command/exit code and dependency availability recorded.
2. **Discovery before execution:** list tests with each exact project/filter. Expected component counts are 14 existing Facts and 16 proposed new Facts after implementation. The separate existing Unit route Theory has four InlineData cases; expect four expanded cases and keep its discovery/results separate. Record expanded actual names/counts. Zero, missing, or unexpected discovery fails until the selector/change is reviewed; a class-name guess is not proof.
3. **Behavioral results:** TRX/log output for the selected existing and new gates. Include source/service call counters proving zero pre-Search work and cancellation/stale-result races.
4. **SB06 continuity:** its server scope/authorization/profile recheck and bounded query tests remain valid for the final UI-facing contracts.
5. **Browser artifacts:** reviewed normal and open-overlay images, route/viewport/scroll notes, Enter-key provider-save counter proof, and settings Load/Apply/confirmation behavior.
6. **Architecture evidence:** changed project-reference graph, logical type-size/method review, old provider-form/overview-loading body removal, and approved exception record if thresholds are exceeded.

Run the following only during execution after the implementation and test project build. The existing list is source-verified; no test command below was run during preparation.

```powershell
$sb07ComponentProject = 'C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj'
$sb07ExistingCases = @(
    'CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.Toolbar_is_icon_only_and_connections_load_only_when_opened'
    'CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.Publication_settings_do_not_render_or_load_source_connections'
    'CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.Compact_filter_can_be_cleared'
    'CanDoItAll.Tests.Components.AgentFramework.AgentProviderProfilesPanelPricingTests.Provider_editor_surfaces_model_prices_on_dedicated_prices_tab'
    'CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests.Hr_agent_avatar_action_remains_in_the_page_header_across_module_tabs'
    'CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests.Simple_chats_follows_agents_and_renders_both_nested_workspaces'
    'CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests.Usage_scope_defaults_to_both_and_is_forwarded_to_detail_dialogs'
    'CanDoItAll.Tests.Components.ProjectStructure.ProjectStructurePageDatabaseSwitchTests.Manager_summary_tab_query_selects_an_explicitly_lazy_report'
    'CanDoItAll.Tests.Components.ProjectStructure.ProjectStructurePageDatabaseSwitchTests.Manager_summary_snapshot_survives_server_rendered_tab_disposal'
    'CanDoItAll.Tests.Components.ProjectStructure.ProjectStructurePageDatabaseSwitchTests.Manager_summary_activity_dialog_is_created_only_after_explicit_open'
    'CanDoItAll.Tests.Components.Shell.SettingsPageDataSourcesTests.Legacy_provider_settings_url_redirects_to_authoritative_agents_tab'
    'CanDoItAll.Tests.Components.Shell.SettingsPageDataSourcesTests.Settings_page_renders_data_sources_tab_with_saved_profiles_and_editor_actions'
    'CanDoItAll.Tests.Components.ApiTokenAdministrationTests.TOKEN_ADMIN_list_is_lazy_and_revoke_delete_require_confirmation'
    'CanDoItAll.Tests.Components.ApiTokenAdministrationTests.TOKEN_ADMIN_access_denial_prevents_data_loading_and_rechecks_every_action'
)
$sb07ExistingFilter = ($sb07ExistingCases | ForEach-Object {
    'FullyQualifiedName=' + $_
}) -join '|'
dotnet test $sb07ComponentProject --no-build --list-tests --filter $sb07ExistingFilter
dotnet test $sb07ComponentProject --no-build --filter $sb07ExistingFilter --logger 'trx;LogFileName=sb07-existing.trx'
$sb07NewFilter = 'FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.ProviderRequestHistoryPanelTests|FullyQualifiedName~CanDoItAll.Tests.Components.Shell.ProviderHistoryPolicyPanelTests|FullyQualifiedName=CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.History_actions_do_not_submit_provider_editor'
dotnet test $sb07ComponentProject --no-build --list-tests --filter $sb07NewFilter
dotnet test $sb07ComponentProject --no-build --filter $sb07NewFilter --logger 'trx;LogFileName=sb07-history-policy.trx'
```

The proposed new filter must be replaced with the actual implemented owner/name set before claiming its expected 16-case discovery. Do not run broad real-provider browser classes that issue tokens or invoke paid providers as an incidental proof shortcut.

Run the separate existing Unit route regression only during execution after its project build. This exact filter selects one source-verified Theory with four InlineData cases; it does not widen the gate to the entire class or Unit project. No discovery or test execution was performed during preparation.

```powershell
$sb07UnitProject = 'C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj'
$sb07UnitRouteFilter = 'FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkSimpleChatsRouteTests.UsageScopeHasTypedDeterministicParsing'
dotnet test $sb07UnitProject --no-build --list-tests --filter $sb07UnitRouteFilter
dotnet test $sb07UnitProject --no-build --filter $sb07UnitRouteFilter --logger 'trx;LogFileName=sb07-route-unit.trx'
```

Record all four expanded route cases and their results. A different discovery shape requires explicit runner/selector review and proof that all four inputs execute; never count a single undiscovered/untested Theory placeholder as four passing cases.

## Browser Validation Logging

Use the required Browser skill for interactive inspection and the existing Playwright test infrastructure for deterministic regression fixtures. Follow the current runtime owner/permission requirements; do not start or restart an application during preparation. If the required browser runtime is unavailable during execution, record the blocker and leave browser status Not started.

| Route/surface | Required action and assertion | Proposed evidence file |
|---|---|---|
| `/agents?tab=providers` -> select saved provider -> History | First viewport, zero reads before Search, fixed provider scope, no Save on Enter/Search. | `browser/01-provider-history-unrequested-1920x1080.png` and call-counter log |
| `/agents?tab=request-history` | Zero automatic usage/history queries; submitted finite filters; one page; changed draft visibly differs from applied query. | `browser/02-all-provider-history-results-1920x1080.png` |
| Both history hosts | Dropdowns open; long labels; Next/Previous; canceled request; failed request with clearly labeled prior results; no lateral page overflow. | `browser/03-history-filters-and-failure-1920x1080.png` |
| History Details | Metadata only after click; content only after separate click/authorization; correct close/focus/scroll; unsupported/expired state. | `browser/04-history-details-open-1920x1080.png` |
| `/settings?tab=provider-history` | Permission gate; no policy read until Load; typed field errors; version conflict; no history/count read from policy UI. | `browser/05-history-policy-1920x1080.png` |
| Policy confirmation | Explicit bounded preview, readable affected scope/retention warning, cancel leaves settings unchanged, Apply targets current version/profile. | `browser/06-policy-confirmation-open-1920x1080.png` |
| Profile/auth transition | Delayed former-scope completion cannot restore rows/content/policy after identity changes. | `browser/07-scope-change-1920x1080.png` and delayed-request/counter log |

Record actual base URL, selected database profile/fixture identity, route, 1920x1080 viewport, application revision, actions/assertions, and screenshots reviewed. Check the real provider pane beside its 25rem list. Review overlay stacking/clipping, focus/return focus, Escape, stable footer actions, first-viewport usefulness, and scroll ownership. Capture no raw token, prompt secret, signed URL, or provider configuration.

Existing selectors: `providers-tree-provider`, `provider-editor-tab-sharing`, `provider-editor-tab-prices`, `providers-save`, and `agents-shell-tabs`. Proposed selectors: `provider-editor-tab-history`, `provider-history-panel`, `provider-history-search`, `provider-history-not-requested`, `provider-history-results`, `provider-history-details-dialog`, `provider-history-load-content`, `provider-history-policy-load`, `provider-history-policy-apply`, and `provider-history-policy-confirmation`. SecondaryTabs currently emits buttons, not ARIA tabs: scope the Request history button by its accessible name within the existing host.

## Progression Gate

- SB08 may consume SB07 only after both hosts, policy UI, existing regressions, all accepted new cases, zero-load call counters, provider-form keyboard proof, current SB06 security/query proof, desktop/overlay review, and architecture checks pass at one recorded application revision.

A successful render or build alone does not pass this phase. A missing browser connection, unverified server permission recheck, empty test discovery, unresolved form ownership, or unsupported component workaround leaves this phase Not started/incomplete rather than silently waiving the proof. No downstream closure may describe the tabs as verified until this gate is recorded.

## Reopen Triggers

- Change to scope, TimeBasis/SortAtUtc/EntryId, cursor live-walk semantics, pricing evidence, or query/detail/policy DTOs.
- Change to history privileges, host local-operator handling, owner authorization, profile fence, or before-publish revision checks.
- New automatic history/count/facet/canonical-content query from a page lifecycle or control.
- Move back into provider EditForm, changed provider EditContext lifetime, or altered Settings Apply/version semantics.
- A new module/project edge, shared BaseLib change, oversized logical type, or broad route/shell refactor.
- SB08 finds a mismatch between source/consumer request attribution, canonical detail, retained lifetime, or the UI's displayed scope. Recheck the affected SB06/SB07/SB08 surfaces.

## Suggested Agent Prompt

```text
Implement SB07 only after SB03 and SB06 gates pass.
Use the final neutral history contracts and existing components. Build the two scopes of one explicit-search feature, isolate the provider form, and place the policy editor in Workspace without a reverse module dependency. Preserve zero pre-Search reads, independent policy Load/Apply, server and UI scope/authorization checks, live cursor semantics, and bounded explicit content. Run the exact discovered focused tests and controlled 1920x1080 normal/overlay proof, record actual evidence, and stop if the progression gate cannot pass. Do not broaden runtime capture, canonical retention, billing, IDM, or provider wire behavior.
```

## C# Architecture Impact

The phase introduces small UI/controller/form owners, not a new runtime history manager. Existing AgentsHomePage is 371 Razor plus 923 code-behind lines; ProviderProfilesPanel is 280 plus 451. They receive tab/branch/ownership calls, not search, authorization, persistence, or policy engines.

Apply the central thresholds: new history classes normally stay within 250 lines; above 250 requires responsibility review and above 400 requires a written redesign/exception gate. Count logical types across Razor/code-behind, not just each physical file. A component/controller extracted from old code must replace that old responsibility rather than leave a second implementation.

## Boundary Ownership

| Owner | Owns | Excludes |
|---|---|---|
| AgentFramework `ProviderRequestHistoryPanel` | Shared search composition and typed action binding for both scopes. | EF, source file reads, pricing arithmetic, raw provider config, token registry. |
| `ProviderRequestHistorySearchController` | One bounded search session, draft/applied state, generation/cancellation, cursor and safe error state. | Global caches, mutable shared principal state, source-specific reconstruction, UI markup. |
| `ProviderRequestHistoryDetailsDialog` | Explicit metadata/content disclosure and its disposal. | Full transcript duplication, permission inference, unrelated owner scans. |
| `ProviderProfileEditorForm` | Existing provider form/context/validation/footer boundary around editable panes. | History/search state, policy operations, duplicated field markup. |
| Workspace `ProviderHistoryPolicyPanel` | Authorized explicit Load, draft validation, versioned Apply/confirmation UI. | AgentFramework UI imports, EF, retention execution, capture-mode side effects on field edit. |
| SB06 application/host services | Scope/resource authorization, query/deadline/cursor rules, policy operations and before-publish recheck. | Component render state or a trust decision from a submitted UI flag. |
| SB03/SB05 persistence/owner services | Policy durability, canonical lifetime, retention/quota, versioned projection/deletion. | UI lifecycle callbacks or a second transcript store. |

## Dependency Direction

- AgentFramework UI -> approved neutral ProviderHistory Abstractions query/detail/access contracts.
- Workspace Settings -> approved neutral ProviderHistory Abstractions policy/access contracts.
- Web/composition -> existing host policy implementation and approved application/persistence registrations.
- No Workspace -> AgentFramework/ProviderManagement/UI edge; AgentFramework already references Workspace.
- No neutral Abstractions/Application/Persistence -> either UI module.
- No direct component -> AppDbContext, canonical repository/file store, provider driver, or token registry call.
- Do not repurpose SettingsRendererRegistry/SettingsRendererHost into a generic feature plugin just to place this one policy panel. Existing conditional Settings composition plus neutral ports is sufficient.

## Pattern Decision

| Decision | Reason |
|---|---|
| One typed feature component with two host scopes | The behavior is identical; scope is explicit data, not duplicated pages or name-based filters. |
| Plain controller for search state | Cancellation, draft/applied values, and stale completion are non-trivial and independently testable. No interface for that trivial UI controller is needed. |
| Cohesive ProviderProfileEditorForm wrapper | It creates a real mutation/form boundary and factors the footer; it is not cosmetic partial-file splitting. |
| Existing neutral query/detail/policy ports | They are real security/application boundaries supplied by upstream phases; do not invent separate UI repositories. |
| Controlled detail/confirmation dialogs | Their content/lifecycle loads only when explicitly opened and does not displace the request table. |
| Reject global result caching and generic object payload visitors | They hide ownership/authorization and increase memory/transcript duplication without helping this user task. |

## Testability Contract

Use recording query/detail/policy ports, a controlled clock where time matters, and deterministic completion sources for races. Do not add sleeps or tests that merely restate component parameters. Keep authorization assertions on the real service boundary in SB06 and verify that UI actions use that boundary.

The following **16 Fact cases are proposed**, not currently discovered tests. They can be renamed during implementation only with the actual owner, final selector, and discovery expectation updated before the gate.

| Proposed owner | Proposed cases |
|---|---|
| `CanDoItAll.Tests.Components.AgentFramework.ProviderRequestHistoryPanelTests` | `Provider_history_remains_unrequested_until_search`; `All_provider_history_remains_unrequested_until_search`; `Draft_filter_edits_do_not_fetch_and_preserve_applied_query`; `Search_paging_uses_applied_filters_and_fixed_scope`; `Host_scope_change_cancels_and_discards_previous_results`; `Profile_or_authorization_change_discards_late_results`; `Canceled_and_failed_queries_do_not_become_empty_success`; `Details_require_explicit_open_and_content_request`; `Typed_time_and_pricing_evidence_is_not_fabricated`. |
| Existing `CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests` | `History_actions_do_not_submit_provider_editor`; pair it with browser Enter-key proof because synthetic component events alone do not prove native implicit submission. |
| `CanDoItAll.Tests.Components.Shell.ProviderHistoryPolicyPanelTests` | `Policy_is_not_loaded_until_explicit_open`; `Policy_access_denial_prevents_reads_and_updates`; `Policy_apply_preserves_version_and_reports_conflict`; `Retention_shortening_requires_explicit_preview_and_confirmation`; `Policy_apply_never_queries_or_purges_history_implicitly`; `Profile_or_authorization_change_cancels_policy_work`. |

Add coverage within those owners for invalid range/page limits, Light/unsupported/expired content, bounded payload rendering, live paging/Refresh discovery, and context changes during Details/Apply. If separate cases are needed, record the increased expected count and discovery; do not quietly keep the stale 16-case expectation.

## Partial Class Policy

No new runtime partial or feature partial is permitted as a size workaround. The existing Razor-generated/handwritten page partials may receive small composition edits, but all their lines still count as one logical component. New search/policy controllers are distinct top-level owners. Do not add AgentsHomePage.History.cs, ProviderProfilesPanel.History.cs, or equivalent files containing the whole new feature.

## Architecture Proof Required

- Record before/after project references and prove there is no Workspace/AgentFramework cycle or neutral-to-UI edge.
- Review every new type's responsibility, dependencies, lifetime, and size together with the changed existing logical page types.
- Show that provider form/Save ownership was moved, not duplicated, and that its regression tests and keyboard proof pass.
- Show all history/policy I/O enters the approved application boundary from an explicit action, with zero-call and delayed-completion tests.
- Confirm DTOs/rows contain only bounded safe display facts; no transcript buffers, credential values, provider configuration, EF navigation graphs, or unbounded cached pages.
- Keep current SB03/SB06 evidence for policy concurrency, authorization/profile recheck, cursor validation, and canonical owner access. If those contracts changed, reopen their gates.
- Record the architect review result and evidence paths in the phase execution report. Architecture acceptance remains Not started until those checks actually run.
