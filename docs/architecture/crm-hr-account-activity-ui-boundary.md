# CRM / HR account summary and activity history UI boundary

Execution record of the second bounded CRM / HR extraction on `components-decoupling`: the
CRM account summary and the activity history timeline now render from
`src/UI/CanDoItAll.CrmHr.UI`, the module keeps thin compatibility adapters under the existing
component names, and every read, mutation and navigation stays with the pages. Only these two
areas moved; the CRM / HR module is not declared decoupled. The Home overview is recorded in
[CRM / HR Home UI boundary](crm-hr-home-ui-boundary.md).

## Ownership

| Concern | Owner |
|---|---|
| Account summary rendering (record header, stage and lifecycle badges, contact and count strip, directory and conversion actions, no-account state) | `CanDoItAll.CrmHr.UI/Accounts/CrmHrAccountSummarySurface` |
| Timeline rendering (heading and description, whole-history counts, loading and empty states, rows with kind, tone, overdue flag, metadata and timestamp, pager) | `CanDoItAll.CrmHr.UI/Activity/CrmHrActivitySurface` |
| Presentation records and intents | `CrmHrAccountSummary`, `CrmHrAccountSummaryIntent`, `CrmHrActivityPage`, `CrmHrActivityEntry`, `CrmHrActivityCopy`, `CrmHrActivityIntent` (rendering library) |
| Projection of module models into presentation | `CrmHrAccountSummaryMapper`, `CrmHrActivityPresentationMapper` (module) |
| Compatibility adapters under the current names and parameter shapes | `AccountSummaryPanel` (`Account`, `OpenDirectory`, `MarkActiveCustomer`, `IsInteractive`), `InteractionTimeline` (`Page`, `IsLoading`, `PageRequested`, wording, `DataTestId`) |
| Reads, loading and failure states, page requests, generation fencing | `CrmHrCrmPage` (account activity), `CrmHrDirectoryPage` (party history), `CrmHrWorkforcePage` (workforce history), unchanged |
| Directory navigation and the "convert to active customer" mutation | `CrmHrCrmPage` (`OpenDirectory`, `MarkActiveCustomerAsync(Guid)` → `SaveAccountProfileAsync` through `CrmService`) |
| Query owners | `CrmService.GetAccountWorkspaceAsync`, `CrmService.SearchAccountActivityAsync`, `PartyDirectoryService.SearchPartyActivityAsync`, unchanged |

### Contract decision

The presentation records stay private to the rendering library and its hosts, as for Home; no
contracts assembly was added. The library's public inputs carry no module type: no
`CrmAccountWorkspaceModel`, no mutable `CrmAccountProfileEditorModel`, no
`CrmActivityHistoryPage`, no domain enum. Labels are the enum names the module always showed
(`RelationshipStage.ToString()`, `LifecycleStatus.ToString()`), tones are typed
(`CrmHrAccountTone`, `CrmHrActivityTone`) and resolved by the module mappers with the module's
own switch tables, and the conversion offer is a presentation flag
(`CanConvertToActiveCustomer`) computed from the relationship stage. The timeline keeps the
query owner's paging contract: `TotalCount`, `ActionCount` and `OverdueActionCount` cover the
whole history, `Items` only the requested page, `TotalPages` derives from them exactly as
before.

The conversion is an intent, not a surface behavior: `ConvertToActiveCustomer(accountPartyId)`
names the account it was raised for. The adapter drops it unless it still shows that account,
and the page admits it only for its selected account and only after `TryCaptureCrmAction`
succeeded; the profile editor is changed after the capture, so a stale callback neither saves
nor leaves the form half-edited (before, the editor's stage was set before the capture guard
ran). The page's form-submit path shares the same capture and save code.

### Host-owned interactivity

`CrmHrCrmPage` renders the account dialog during prerendering when the route names an
account, so the conversion button exists before the circuit can handle a click. The page flips
`IsInteractive` after its first interactive render (`OnAfterRender` never runs during
prerendering), the summary reports `data-interactive` on its root and keeps its actions
disabled until then. The same signal was added to the Home host in this pass; the timeline
keeps its pager gated by the host's loading flag only.

## Behavior matrix

Classification: **Preserve** = current behavior kept; **Safeguard** = authorized isolation
change covered by tests; **Correction** = explicit observable change recorded here.

| Behavior | Before → after | Class | Test |
|---|---|---|---|
| No-account state with its copy and "Open directory" action | same copy through the surface | Preserve | surface `No_account_renders_the_empty_state…`, adapter `AccountSummaryPanel_without_an_account…` |
| Account header: name, optional summary, relationship stage and lifecycle badges with the module's tones, "Open directory record" | same labels and tones, now typed through the mapper | Preserve | mapper `Maps_labels_tones…`, tone theories (7 lifecycle + 4 stage cases), surface `Account_renders_labels_tones…`, adapter, browser lane |
| Contact copy (`Not set`), roles (`None` or joined), connection, opportunity and tag counts | same | Preserve | surface `Account_renders_labels…`, `An_active_customer_is_not_offered…` |
| "Convert to active customer" offered only when the stage is not `ActiveCustomer` | same rule, now a presentation flag | Preserve | mapper, surface, sandbox `Active_customer_scenario…` |
| Conversion is a page-owned mutation fenced to the selected account and the current query load; the editor is changed only after the action was captured | editor changed before the capture guard | Safeguard | adapter `…forwards_the_conversion_only_for_the_shown_account`, browser `Converting_a_prospect_saves_the_profile_from_one_click…` (persisted stage asserted through the service) |
| Prerendered conversion button could receive a lost click | `IsInteractive` signal, actions disabled until the first interactive render | Correction | surface `A_non_interactive_host_reports_the_signal…`, browser conversion lane (one click after `data-interactive="true"`) |
| Timeline heading, description and empty copy per host (CRM default, Directory party history, Workforce history) | same strings, passed as `CrmHrActivityCopy` by the adapter | Preserve | adapter `InteractionTimeline_maps_the_history_page_keeps_the_host_copy…`, sandbox `The_three_timeline_hosts…` |
| Whole-history counts (`N activities`, `N next actions`, `N overdue` only when positive) | same | Preserve | mapper `Maps_page_fields_whole_history_counts…`, surface `Page_renders_totals…`, `A_single_page_without_overdue_actions…` |
| Row order as delivered by the owner; kind badge with tone; Overdue badge; metadata line; local short timestamp | same (`LocalDateTime.ToString("g")` kept) | Preserve | mapper (order, tone parsing with neutral fallback, blank description absent), surface `Page_renders_totals_items…` |
| Pager text, bounds, disabled first/last and loading states; a page request outside the range is ignored | same | Preserve | surface `A_single_page…`, `Loading_shows_the_loading_state…`, adapter (page request forwarded as the same `int`) |
| Host-owned loading and failure: the pages keep their loading flags, error cards and retry; the surface renders the page it is given | same | Preserve | adapter (loading flag), existing page facts in the CRM / HR Components topic |
| A new selected account never shows the previous account's rows | `InvalidateAccountActivity` on the page (unchanged) resets the page; the adapter re-maps on every new page reference | Preserve | existing page facts; adapter re-maps by reference |
| Test identifiers: `crmhr-account-convert-active-button`, `<host>-item`, `<host>-loading`, `<host>-pager`, `<host>-previous`, `<host>-next` kept; new `crmhr-account-summary` (with `data-account-id`, `data-interactive`), `crmhr-account-summary-empty`, `-name`, `-text`, `-stage`, `-lifecycle`, `-open-directory`, `<host>` root (with `data-page-index`, `data-total-pages`, `data-loading`), `<host>-totals`, `<host>-overdue-total`, `<host>-empty`, row `data-kind` and `data-overdue` | additive | Preserve | surface and browser lanes |
| Data text is encoded as text; long values wrap inside their row | Razor encoding; `break-words` on the row texts and the summary header | Preserve | surface `Untrusted_text…`, sandbox `Long_text_scenario…`, browser overflow probe at 1100 px |

No new read, aggregation, route, URL contract or mutation was added; the three timeline hosts
and their query owners are unchanged.

## Consumers

- `CrmHrCrmPage` (`/crm-hr/crm`) composes `AccountSummaryPanel` in the account dialog and
  `InteractionTimeline` (`crmhr-account-activity`) in the Interactions tab.
- `CrmHrDirectoryPage` (`/crm-hr/directory`) composes `InteractionTimeline`
  (`crmhr-directory-activity`) in the Activity tab.
- `CrmHrWorkforcePage` (`/crm-hr/workforce`) composes `InteractionTimeline`
  (`crmhr-workforce-history`) in the History tab.
- The sandbox specimen `/crm-hr/account-activity` composes `CrmHrAccountSummarySurface` and
  the three timeline compositions directly.

## Validation

Commands run from the repository root in Release with `/m:1`; discovery counts were stated
before execution. See the execution record for the run sequence.

| Slice | Filter | Expected / discovered | Provider | Result |
|---|---|---|---|---|
| Mappers | `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrAccount\|…CrmHrActivity` | 24 / 24 (account: 2 facts + 7 lifecycle-tone + 4 stage-tone cases; activity: 3 facts + 8 tone-token cases) | pure | see execution record |
| Surfaces, adapters, sandbox, boundary | `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrAccount\|…CrmHrActivity` | 31 / 31 (5 account surface + 5 activity surface + 3 adapter + 13 sandbox + 5 boundary) | bUnit | see execution record |
| Whole CRM / HR Components topic (the three pages compose the adapters) | `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.` | see execution record | bUnit and `ComponentTestHarness` on PostgreSQL | see execution record |
| Production browser lane | `FullyQualifiedName~CrmHrAccountActivityBrowserTests` | 2 / 2 | real Web host, PostgreSQL, seeded through the application services | see execution record |

## Readiness

| Dimension | State | Evidence |
|---|---|---|
| Semantic ownership | Proven | reads, loading, failure, paging and the conversion mutation stay with the pages; the adapter fences a conversion intent to the shown account and the page to its captured action |
| Deterministic rendering | Proven | both surfaces render from explicit records in bUnit with no service; the sandbox renders them with local state |
| Scenario interactions | Proven | sandbox scenarios listed in its README; per-host paging and the conversion intent update local state and the intent line |
| Lightweight compile graph | Proven for the direct reference set | `CrmHrAccountActivityUiBoundaryTests` guards the surfaces' parameters, the absence of `[Inject]`, the library's references, and the composition of the same renderer types by the adapters and the sandbox |
| Browser sandbox | see execution record | in-app browser probes of `/crm-hr/account-activity` at the matched and constrained frames |
| Production routes | see execution record | real host and PostgreSQL: seeded account and interactions in the CRM workspace, the Directory timeline host, the conversion mutation |

## Open items

- The Workforce timeline host is covered by the adapter and sandbox lanes and by the existing
  Workforce page facts; the browser lane exercises the CRM and Directory hosts.
- The CRM page's other panels (financials, connections, opportunities, interaction form) keep
  their module rendering.

## Execution record (2026-09-16)

- Start: `8bc0d02eac3dae4e001c5db369e465d304d90ea4` on `components-decoupling`, clean tree,
  after the two review repairs of the same pass (Gallery picker context policy, Home
  lifecycle evidence). Nothing was pushed, merged, rebased or reset; no signing or permission
  configuration was touched.
- Source: `Accounts/` and `Activity/` in the rendering library, the two module mappers, the
  two adapters rewritten over the shared surfaces, the CRM page's conversion guard and
  interactivity flag, the sandbox fixture and specimen, and the `_Imports` entries. The
  Directory and Workforce pages were not edited; they compose the adapter unchanged.
- Sandbox observation in the desktop app browser (Debug, Parity assets), computed-style
  probes because the hidden pane does not paint screenshots: `populated` at the matched
  1600×1000 frame (1419 px content width): summary and the three timelines span the frame,
  the stat strip stays on one row, no document or row overflow, totals `12 activities / 3 next
  actions / 1 overdue`, two pages of ten. Clicking "Convert to active customer" withdrew the
  offer and showed `ActiveCustomer`; clicking the Directory timeline's Next moved only that
  host to page 2 of 2 (two rows) and the intent line recorded `Request page: Directory 2`.
  `long-text` at the constrained 960 px frame: markup-looking name, summary, titles and
  metadata rendered as text (no `script`, `b` or `i` elements), no overflow in the summary,
  the stat strip or any row; the stat strip wrapped to two rows. A first probe showed the
  cards shrunk to fit-content because the specimen's frame `Stack` aligned its children to
  the start; the specimen now stretches them like the CRM dialog does.
- Validation (Release, `/m:1`, counts stated before execution and matched; the Unit and
  Components lanes were run once on the whole pass and again after the last source edits,
  see the final rerun below):
  - Unit `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrAccount|…CrmHrActivity`
    24 / 24 passed.
  - Components `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrAccount|…CrmHrActivity`
    31 / 31 passed.
  - Components `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.` 171 / 171 passed
    (2 m 50 s; includes the Home topic 30, the composition baseline 2, `CrmHrNavigationTests`
    13 and the `ComponentTestHarness` facts of the CRM, Directory and Workforce pages).
  - Browser lanes on the real Web host and PostgreSQL (Playwright solution rebuilt for the
    final source): `FullyQualifiedName~CrmHrAccountActivityBrowserTests` 2 / 2 passed (11 s)
    after two test-side corrections, each rerun from a rebuilt solution: the lifecycle label
    is asserted as the query owner reports it after the profile save (saving a Prospect
    profile reports the party as `Prospect`, not the seeded `Active`), and the interaction
    rows are matched by `data-kind="Interaction"` because logging an interaction also writes
    an audit row that quotes the subject. `FullyQualifiedName~CrmHrHomeBrowserTests` 1 / 1
    and `FullyQualifiedName~PromptGalleryBrowserTests` 4 / 4 passed in the same lane. Three
    captures (account and activity at 1600×1000 and 1100×900, Directory timeline) are in the
    git-ignored Playwright output folder (crm-hr-account-activity).
  - Final rerun after the last source edits (sandbox fixture wording, specimen frame
    alignment, browser-lane matching), Unit and Components solutions rebuilt: Unit Home
    13 / 13, Unit account and activity 24 / 24, `PromptGalleryPickerButtonTests` 6 / 6,
    Components Home 30 / 30, Components account and activity 31 / 31, all passed.
- Static gates: the portability tooling self-tests passed (6 + 4). Portability-static ran on
  the complete tree without `--tracked-only`, the new untracked files included. The first scan
  reported two `ADDED` findings in `CrmHrAccountActivitySandboxFixture.cs`: a `process-domain`
  hit on the word "escalation" in a synthetic interaction description (a false match of the
  process-domain rule on prose; the fixture text was reworded and the finding disappeared) and
  a `case-policy` hit on the scenario token parsing with `OrdinalIgnoreCase`, the same reviewed
  pattern the Home and Prompts sandbox fixtures carry in the baseline. The baseline was
  refreshed for that single entry, its diff inspected (one added entry, count 14682 → 14683),
  and the final enforcement without `--write-baseline` on the final tree reports
  `PASS (14683 reviewed executable-source findings unchanged)`. `Test-Documentation.ps1`
  passed (221 maintained Markdown files) after the final update of this record.
- Not run: the broad Stable gate. No solution, project set, `Directory.Build.*`, shared
  persistence, migration or test-infrastructure file changed in this pass (the rendering
  library and the sandbox already existed; only files inside them were added), so no named
  trigger arose. The historical `SecretScanningTests` failure on ignored retained artifacts
  and the load-sensitive canvas preview fact recorded in the Home record are unchanged and
  not cleared by this pass.
- Tree: every change of the pass is in the three signed commits listed in the final report
  (Gallery picker policy, Home evidence repairs, account summary and activity extraction);
  nothing outside the pass was modified.
