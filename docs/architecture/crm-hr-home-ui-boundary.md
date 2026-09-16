# CRM / HR Home UI boundary

Maintained record of the CRM / HR Home overview extraction on `components-decoupling`:
architectural decisions, the behavior matrix, the validation performed, measurements and open
items. This record covers the Home overview at `/crm-hr` only; Directory, CRM, Workforce,
Recruiting, Agents and Assignments stay in the module unchanged.

## Ownership

| Responsibility | Owner | Notes |
|---|---|---|
| Rendering of the Home overview: header stats, directory focus, route coverage, privacy posture, open pipeline, loading and failed states | `src/UI/CanDoItAll.CrmHr.UI` (`CrmHrHomeSurface`) | Controlled surface: one `CrmHrHomePresentation` in, typed `CrmHrHomeIntent` out; no `[Inject]`; the secondary navigation is a host-owned `SecondaryNavigation` slot |
| Home presentation records and text helpers | `src/UI/CanDoItAll.CrmHr.UI` | `CrmHrHomeOverview` (totals + three preview lists, copied on construction), `CrmHrHomeDirectoryEntry`, `CrmHrHomeSensitiveEntry`, `CrmHrHomeOpportunityEntry`, `CrmHrHomePhase`, `CrmHrHomeDestination` |
| Route, document title, static agent-context surface, navigation, read lifecycle | `CrmHrHomePage` (module) | `@page "/crm-hr"`, `PageTitle`, `AgentChatContextSurfaceProvider` with `CrmHrAgentChatSurfaceBuilder.BuildHomeSurface()`, `CrmHrRouteCatalog` mapping, `ILogger` for failures |
| One accepted snapshot per host instance, Loading/Ready/Failed, retry admission, fencing of late results | `CrmHrHomeReadSession` (module) | Per instance, never a scoped or singleton service; passes its lifetime token to the query; no refresh feature |
| Projection of the application snapshot into presentation records | `CrmHrHomePresentationMapper` (module) | Totals from the owner, labels formatted at the boundary, sensitive rows without summary, lists copied |
| Query eligibility, sorting, totals and the 5 / 3 / 6 preview limits | `CrmHrHomeQueryService` (module) | Unchanged |
| Secondary tabs, route catalogue, workbench identities | `CrmHrSecondaryTabs`, `CrmHrRouteCatalog` (module) | Unchanged; composed by the host into the surface's slot |
| Backend-free scenario host | `src/Sandboxes/CanDoItAll.CrmHr.UiSandbox` | Renders the same `CrmHrHomeSurface` with deterministic local state and an intent line |

### Contract decision

Home renders totals, safe preview values, labels, optional numbers and two typed identifiers.
The snapshot and preview records of the query service are declared beside the EF-backed
query and carry module-owned enums (`PartyType`, `PartyLifecycleStatus`, `OpportunityStage`,
`OpportunitySource`) and `OpportunitySummaryModel`. Referencing them from the renderer would
retain the CRM / HR implementation assembly and its graph (Infrastructure, AgentFramework,
Workspace, Projects, Memory, AppComponents, Charts, Gantt). A small Home-specific presentation
contract was therefore preferred over extracting a `CrmHr.Contracts` family: the renderer
declares its own records, the host projects into them and formats the enum labels (the enum
names the page has always shown) at the boundary. Actions stay structured: a destination enum
and the account plus opportunity identifiers, never a route string in display text.

Evaluated compile graph of `CanDoItAll.CrmHr.UI` (project and package references):
`CanDoItAll.Components.BaseLib` → `CanDoItAll.Components.Common`; `Microsoft.AspNetCore.Components.Web`.
The sandbox references only the rendering library. `CrmHrHomeUiBoundaryTests` asserts the
*direct* referenced-assembly set of the library, the absence of `[Inject]` on every component,
that public signatures expose no other assembly, and that the production page assembly and the
sandbox assembly both reference the renderer assembly while the sandbox does not reference the
module. It is not a recursive proof of the transitive graph, which is stated here from the
evaluated references.

### Host-owned chrome and context

`CrmHrSecondaryTabs` navigates and depends on `CrmHrRouteCatalog`; it stays in the module and
is rendered by `CrmHrHomePage` through the surface's `SecondaryNavigation` slot. The sandbox
renders a labelled placeholder in that slot instead of duplicating the tabs. The agent-context
registration stays with the host: the static Home surface (route, surface and view identity,
display name) is published by the existing `AgentChatContextSurfaceProvider`, which activates
its scope on first parameters and releases only that scope on disposal. No preview row,
sensitive value or error text is ever added to the context.

## Behavior matrix

Classification: **Preserve** = current behavior kept; **Safeguard** = authorized loading and
lifecycle change covered by tests; **Correction** = explicit observable change recorded here.

| Behavior | Before → after | Class | Test |
|---|---|---|---|
| Entry point `/crm-hr`, document title `CRM / HR`, Home secondary tab, navigation tooltip, workbench identity | unchanged | Preserve | `CrmHrHomePageTests.Host_renders_the_shared_surface…`, `CrmHrNavigationTests` |
| Header totals: parties, organizations, pipeline, workforce, agents from the query owner; sensitive total in the privacy badge; none derived from preview lengths | same values, now through `CrmHrHomeTotals` | Preserve | `CrmHrHomePresentationMapperTests.Totals_come_from_the_snapshot…`, surface `Ready_presentation_renders_totals…`, sandbox `Large_totals_scenario…` |
| Organization count includes Organization and OrganizationUnit; agent count counts bound technical projections only | query owner unchanged | Preserve | Integration `StaffingAllocationIntegrationTests.Home_agent_count_uses_only_bound_agent_framework_projections` (existing) |
| Directory preview: order, name, type / lifecycle labels, optional summary, Sensitive badge; capped at 5 by the owner | same | Preserve | mapper `Directory_rows_keep_order_labels…`, surface `Ready_presentation…` |
| Sensitive preview: order, name, type / lifecycle, Sensitive badge, no operational summary; capped at 3 | same, the presentation record has no summary field | Preserve | mapper (no summary property), surface `Untrusted_text_is_rendered_as_text_and_the_sensitive_card_carries_no_summary`, browser case |
| Open pipeline: title, account, owner, stage / source, nullable amount with probability, owner order; capped at 6, Won/Lost excluded; Unknown account / Unknown owner from the owner | same | Preserve | mapper `Opportunity_rows_keep_both_identities…`, surface `Ready_presentation…` |
| Navigation: Directory (header, route card, sensitive card, empty state), CRM (route card, pipeline card), Workforce, Recruiting, Agents, Assignments through the route catalogue | `NavigateTo` literals → intents mapped by the host through `CrmHrRouteCatalog` | Preserve | surface `Every_navigation_action_emits_its_destination…`, host `…maps_every_navigation_to_the_catalogue_exactly_once` |
| Opportunity detail `/crm-hr/crm?accountId=<account>&opportunityId=<opportunity>` | same, built by the host | Preserve | surface `…the_opportunity_action_carries_both_identifiers`, host, browser case |
| Agent context: `BuildHomeSurface()` metadata only; nothing from previews or errors | same | Preserve | host `Host_publishes_the_static_home_agent_context…` |
| Empty sections: section-specific copy after a successful read; zero parties shows the create-party empty state | same copy, now only in the Ready phase | Preserve | surface `Empty_ready_overview_shows_the_section_empty_copy…` |
| Before the first read succeeds the page showed a zero snapshot (zero parties, no opportunities, "0 sensitive record(s)") | explicit Loading phase: placeholder stat values, loading state, no empty copy, route buttons usable | Safeguard | surface `Loading_renders_placeholders…`, host `Host_renders_the_shared_surface…` |
| A failed query threw out of `OnInitializedAsync` | explicit Failed phase with safe English copy, Retry and Open directory; the exception goes to the log; the static context stays published | Safeguard | session `Failed_read_is_failed_with_safe_copy…`, host `Failed_read_offers_retry…`, `Host_publishes_the_static_home_agent_context_keeps_it_on_query_failure…` |
| Retry of a failed read; duplicate and stale retries; a retry of a ready overview is not a refresh | n/a | Safeguard | session `Retry_after_a_failure_reads_again_and_duplicate_or_stale_retries_are_ignored` |
| Same-instance rerender issues no request; a recreated instance starts clean | one read in `OnInitializedAsync` (unchanged) | Preserve | session `A_second_load_of_the_same_instance_issues_no_request`, host `Two_live_instances_keep_separate_state…` |
| Cancellation passed through the query port; disposal ignores late success and failure of a query that ignores cancellation | no token before | Safeguard | session `Disposal_ignores_a_late_outcome…`, host `A_disposed_host_ignores_the_late_outcome…` |
| Presentation data is copied; entities and editor models never reach the renderer | n/a | Safeguard | mapper `Overview_copies_the_supplied_collections`, boundary tests |
| Data text is encoded as text | Razor encoding (unchanged) | Preserve | surface `Untrusted_text_is_rendered_as_text…`, sandbox `Long_text_scenario…` |
| Appearance: section hierarchy, existing selectors (`crmhr-home-open-directory`, `crmhr-home-sensitive-card`, `crmhr-home-sensitive-open-directory`, `crmhr-home-opportunity-item`), theme, responsive layout | same markup and classes; new selectors added for rows, states and actions | Preserve | browser case at 1600×1000 and 1100×900 |
| The Sensitive badge in a directory or sensitive row broke mid-word ("Sensitiv / e") when the row was squeezed at 1100 px | badge no longer wraps (`NoWrap`) | Correction | browser capture at 1100×900 |

No new currency policy, aggregation, card, filter, polling, route or URL-state schema was
added. The query service is unchanged.

## Consumers

- `CrmHrHomePage` (`/crm-hr`) renders `CrmHrHomeSurface` with the production
  `CrmHrSecondaryTabs` in its slot and the static agent-context provider beside it.
- The sandbox renders `CrmHrHomeSurface` directly with fixture presentations.
- No other CRM / HR page, the route catalogue, the agent-context builder or the query service
  changed.

## Validation

Commands run from the repository root in Release with `/m:1`. Discovery counts were stated
before execution.

| Slice | Filter | Expected / discovered | Provider | Result |
|---|---|---|---|---|
| Read session and mapper | `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrHome` | 12 / 12 (8 session + 4 mapper) | scripted query | 12 passed |
| Surface, host, sandbox, boundary | `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrHome` | 28 / 28 (7 surface + 6 host + 12 sandbox + 3 boundary) | bUnit, scripted query, real in-memory agent-context registry | 28 passed (a first run failed 7 cases on stat selectors that targeted the tooltip panel; the surface now wraps each stat in an addressable element) |
| Existing Home composition facts | `FullyQualifiedName=…OpportunityBoardTests.Home_page_surfaces_open_pipeline_preview\|FullyQualifiedName=…CrmHrPrivacyBoundaryTests.Home_and_workforce_routes_surface_sensitive_handling_and_history` | 2 / 2 | `ComponentTestHarness` on PostgreSQL | 2 passed |
| Route mapping and chrome | `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrNavigationTests` | 13 / 13 | bUnit | 13 passed |
| Production browser lane | `FullyQualifiedName~CrmHrHomeBrowserTests` (Playwright solution) | 1 / 1 | real Web host, PostgreSQL, seeded through the application services | 1 passed; three captures (1600×1000 overview, 1100×900 overview, opportunity deep link) in the git-ignored Playwright output folder (crm-hr-home). Two earlier runs failed on test mechanics only: a load-based navigation wait that Blazor's client-side navigation never satisfies, and a click that landed on prerendered markup before the circuit was interactive; the test now polls the URL and clicks with a bounded retry |

Static gates and the broad Stable gate are recorded in the execution record below.

## Measurements

Same machine, Release, `/m:1`, incremental `dotnet build --no-restore`, one appended Razor
comment as the edit. The baseline was captured on the untouched source before any change.

| Measurement | Before extraction (edit in `CrmHrHomePage.razor`) | After extraction (edit in `CrmHrHomeSurface.razor`) |
|---|---|---|
| Module `CanDoItAll.Modules.CrmHr` no-op / after edit | 12.3 s / 13.3 s (warm build 58.7 s) | 9.0 s / 9.1 s (warm build 9.5 s, the module was already warm from the test lanes) |
| Web host `CanDoItAll.Web` no-op / after edit | 19.4 s / 23.7 s (warm build 44.3 s) | 22.4 s / 22.3 s (warm build 21.9 s) |
| Rendering library `CanDoItAll.CrmHr.UI` no-op / after edit | not applicable | 2.0 s / 2.0 s (warm build 1.8 s) |
| Sandbox `CanDoItAll.CrmHr.UiSandbox` (Release, Parity) no-op / after edit | not applicable | 2.1 s / 2.4 s (warm build 2.0 s) |
| Startup to first HTTP 200 | Web 15.7 s (Release, `--no-build`) | sandbox 2.7 s (Release, Parity, `--no-build`) |
| `dotnet watch` startup to first 200 / edit-to-visible (served HTML carries the edited marker) | Web (Debug): 107.2 s / 3.8 s | sandbox (Debug, Parity): 15.1 s / 5.5 s |

The smallest graph that recompiles a Home Razor edit is now the 2 s rendering library or
sandbox instead of the 9 to 13 s module inside the 19 to 24 s Web graph. The Web and module
no-op times vary between runs by a few seconds (the Web no-op was slower after the extraction
than before it), so they are reported, not claimed as an improvement. The Web watch
edit-to-visible time was measured once on a Debug build of the whole host; the sandbox watch
value is a separate measurement recorded below.

## Readiness

| Dimension | State | Evidence |
|---|---|---|
| Semantic ownership | Proven | `CrmHrHomeReadSession` owns one accepted snapshot per host instance with Loading/Ready/Failed, retry admission, cancellation and late-result fencing (8 session cases); the host owns navigation and the static context (6 host cases); the query owner is unchanged (existing integration facts) |
| Deterministic rendering | Proven | `CrmHrHomeSurface` renders from explicit presentation records in bUnit with no service (7 surface cases) and in the sandbox (12 sandbox cases); text is encoded |
| Scenario interactions | Proven | Sandbox scenarios listed in its README; intents reach the intent line only; the failed scenario's Retry resolves locally (bUnit and the in-app browser) |
| Lightweight compile graph | Proven for the direct reference set, stated for the transitive graph | `CrmHrHomeUiBoundaryTests` guards the direct references (BaseLib, Common, Components.Web), the absence of `[Inject]`, public signatures and the shared renderer type; the transitive graph is the evaluated reference set recorded above, not a recursive assertion |
| Browser sandbox | Proven | Served in the desktop app browser with Parity assets (BaseLib CSS, Material Symbols, production theme, sandbox CSS, scoped CSS): populated at the matched 1419 px frame with the real totals and no overflow; long-text at the constrained 960 px frame with markup-looking values rendered as text and no overflowing row after the wrap correction; failed → Retry → ready; no console error after the page load (the only console errors were connection retries during a deliberate server restart). Screenshots could not be captured because the hidden pane never finished painting; the layout facts above come from computed-style probes |
| Production route and bookmarkability | Proven for `/crm-hr` and the opportunity deep link | Real host and PostgreSQL: seeded totals, sensitive card without summary, confidential note absent, Workforce transition, `/crm-hr/crm?accountId=…&opportunityId=…` opens the persisted opportunity; no new URL-state schema was added (none existed before) |

## Open items

- Only Home is extracted; the other six CRM / HR pages keep their module rendering.
- The Home presentation contract is private to the renderer and its host; a second consumer
  would be the moment to decide whether a shared contract assembly is justified.
- The sandbox represents the secondary tabs by a labelled slot; it does not render the module
  tabs and is not a substitute for the production chrome.

## Execution record (2026-09-16)

- Start: `ade8c3d21966d38678d97fe82d04ff753c494676` on `components-decoupling`, clean tree.
  Nothing was pushed, merged, rebased or reset; no signing or permission configuration was
  touched.
- Production projects built (Release, `/m:1`, 0 errors): `CanDoItAll.CrmHr.UI`,
  `CanDoItAll.Modules.CrmHr`, `CanDoItAll.CrmHr.UiSandbox` (Parity), and `CanDoItAll.Web`
  through the Components and Playwright test solutions.
- Test results: the Validation table above. Every lane was rerun once more on the final
  source after the last markup edits (non-wrapping badge, bounded preview boxes with word
  wrapping); those reruns are the counts recorded there.
- Static gates: portability tooling self-tests passed (6 + 4); portability-static ran on
  the complete tree without `--tracked-only` with the new untracked projects included. The
  only delta was one `case-policy` finding in `CrmHrHomeSandboxFixture.cs`: the scenario
  and layout query tokens are parsed case-insensitively with invariant casing, the same
  reviewed pattern the Prompts sandbox already carries in the baseline. The baseline was
  refreshed for that single entry, its diff inspected (one added entry), and the final
  enforcement without `--write-baseline` reports `PASS (14682 reviewed executable-source
  findings unchanged)`; the scan was repeated after the last markup edits with the same
  result. `Test-Documentation.ps1` ran after the final update of this record.
- Browser evidence: the production captures listed in the Validation table (overview at
  1600×1000, overview at 1100×900, opportunity deep link) and the in-app sandbox probes under
  Readiness. The two constrained-width corrections (non-wrapping Sensitive badge, bounded
  preview boxes with word wrapping) came out of those probes; both keep the existing
  shrink-to-fit look for ordinary content.
- Measurements: the table above. Two sandbox probes of the first measurement run failed
  because the launcher split the launch-profile name at its space and the sandbox came up on
  default ports; they were repeated with the profile quoted. No result was reused between
  the runs.
- Broad Stable gate: `CanDoItAll.slnx` gained two projects and the Components test project
  references the new sandbox, which is a named build-graph trigger. The gate is executed once
  at the frozen checkpoint after the commits; its outcome is recorded in the closing bullet
  below. The historical `SecretScanningTests` failure on ignored retained artifacts is not
  cleared by this task.
