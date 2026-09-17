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
| Origin of rendered account actions (review R1): each action captures the record it was rendered for and its intent carries that record; the adapter forwards an intent only when that record is the very instance it currently maps; the page still admits only its selected account after its action stamp | the action lambdas read the live `Account` parameter at click time, so an action created for account A that ran after the surface moved to B read B's identity and passed the adapter and page checks as B; the directory intent's identity was discarded before checking | Safeguard (review repair) | surface `Actions_carry_the_record_they_were_rendered_for…`, adapter `A_conversion_action_rendered_for_one_account_never_converts_the_account_shown_later` and `Retained_actions_are_inert_after_the_account_went_away_and_after_the_same_account_was_loaded_again` (real Button callbacks retained across rerenders, account → null, A → B → A with a new model instance, positive unchanged-target and same-model rerender cases) |
| History page-request admission (review R2): the surface resolves a pager action against the presentation current at the click and drops it while a read is in flight or before a page was accepted; the adapter forwards only admissible requests; each owner's `CrmHrActivityHistorySession` rejects a page request, a duplicate ensure and a retry while its read is in flight, so two reads of one lane never coexist | the surface checked numeric bounds only, the adapter forwarded every index, and the CRM page admitted several page reads under one generation whose completions could land in reverse order | Safeguard (review repair) | surface `A_pager_action_created_earlier_is_resolved_against_the_current_presentation…`, adapter `InteractionTimeline…forwards_a_page_request_only_while_it_is_admissible`, session `A_page_request_keeps_the_accepted_totals_while_it_loads_and_is_rejected_while_a_read_is_in_flight`, `Page_requests_outside_the_accepted_range…` (query-call counts asserted) |
| Truthful history availability (review R3): `CrmHrActivityPresentation` distinguishes not accepted, first read in flight, accepted (empty or populated), another page loading over an accepted history, and the host's failure; unknown totals render as "Counts unavailable" / "Counts load with the history" and "No pages", never as zero; the CRM follow-up pressure card renders the same accepted state and shows a neutral "Follow-up counts unavailable" badge instead of a success-toned zero before anything was accepted; a failed page read keeps the accepted page; a new target never shows the previous target's page | the timeline and the follow-up card always printed the zero-filled `Empty()` page before the first read and after a failed first read | Correction (review repair) | surface `History_not_accepted_yet_shows_no_count…`, `First_read_in_flight_shows_the_loading_state_without_claiming_a_count`, `Another_page_loading_over_an_accepted_history_keeps_its_totals…`, `An_accepted_empty_page_shows_the_host_copy…`, session (initial failure → not accepted, page failure → accepted kept, target change → nothing of the old target shown), sandbox `loading` and `paging`, browser lane (accepted counts on the card, accepted zeros for an account without interactions, no placeholder once accepted) |
| The three timeline owners (CRM account activity, Directory party history, Workforce history) read through one `CrmHrActivityHistorySession` each: target, accepted page, busy gate, retry, generation fencing and cancellation | three inline state machines with the same shape and the R2/R3 gaps | Safeguard (review repair) | session facts (10), the whole CRM / HR Components topic on the real harness, browser lanes for the CRM and Directory hosts |

No new read, aggregation, route, URL contract or mutation was added; the query owners are
unchanged. The three timeline hosts keep their headings, error cards, retry actions, page
size and test identifiers; the Directory host's whole-tab loading state now covers only the
first read of a party (a later page read shows the timeline's own loading state over the
accepted totals), and the Workforce tab badge shows the accepted total only.

### Callback compatibility of the adapters

`AccountSummaryPanel` keeps its name, its `Account` model input and its `OpenDirectory`
callback; `MarkActiveCustomer` is `EventCallback<Guid>` (it carries the identity of the shown
account) since the extraction and is not signature-compatible with the parameterless callback
the pre-extraction component exposed. `InteractionTimeline` keeps its name, its wording inputs
and `DataTestId`; it now takes the owner's `CrmHrActivityPresentation` instead of the raw
`CrmActivityHistoryPage` + `IsLoading` pair, because the presentation carries the accepted
state the review required. The audited consumers are `CrmHrCrmPage`, `CrmHrDirectoryPage`
and `CrmHrWorkforcePage`, all updated in the same commit; no other consumer exists.

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

## Execution record: review repairs R1–R3 (2026-09-17)

- Start: `de7934f3a2b77048a33b244c800406c43e3a9486` on `components-decoupling`, clean tree,
  as the first part of the continuation that then extracts Financials. Nothing was pushed,
  merged, rebased or reset; no signing or permission configuration was touched.
- Reproductions first, through the real adapter, surface and BaseLib `Button`: the retained
  `Button.Click` callback of account A, invoked through the renderer dispatcher after the
  same instance moved to account B, reached the page's `MarkActiveCustomer` callback with B's
  identity (R1); a retained Next callback invoked after the presentation became loading was
  forwarded to the owner (R2); the timeline and the CRM follow-up card printed
  `0 activities` / `0 open follow-ups` / a success-toned `0 overdue` before any read had
  been accepted (R3). The behavior matrix rows above name the corrections and their tests.
- R1: the surface captures the rendered record into every action, the intents carry it, the
  adapter forwards only intents whose record is the instance it currently maps, and the page
  keeps its selected-account and action-stamp admission. An A → B → A lifecycle yields a new
  model instance on the page, so an action from the first A lifetime stays inert while a
  fresh action converts.
- R2 and R3: the three owners read through `CrmHrActivityHistorySession` (target, accepted
  page, busy gate, retry, generation fence, per-read token source disposed only after its
  read returned). The surface and adapter admit a page request only against the current
  presentation; the session rejects further admissions while its read is in flight, so no
  two reads of one lane coexist and reverse completion cannot occur. The presentation
  distinguishes not accepted, first read in flight, accepted, paging over accepted, and the
  host's failure; unknown totals are unavailable, never zero.
- Validation (Release, `/m:1`, counts stated before execution and matched). First run on the
  repaired tree before the Financials work started (the session's own unit class had one
  test awaiting a joined read, which hung the Unit host and was corrected; its stage is
  counted from the rerun), then the whole set again after the Financials sources and the
  corrected tests were in place:
  - Unit `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrActivityHistorySessionTests`
    10 / 10 passed.
  - Unit `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmHrAccount|…CrmHrActivity` 34 / 34
    passed (24 mapper cases + the 10 session facts); Unit Home 13 / 13.
  - Components `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrAccountActivityAdapterTests`
    4 / 4 (real Button callbacks retained across rerenders); Components
    `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmHrAccount|…CrmHrActivity` 38 / 38
    (6 account surface + 8 activity surface + 4 adapter + 15 sandbox + 5 boundary);
    Components Home 30 / 30.
  - The whole CRM / HR Components topic (`FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.`)
    178 / 178 on the repaired tree, then 199 / 199 with the Financials classes added
    (`ComponentTestHarness` on PostgreSQL for the page facts).
  - Browser lanes: recorded with the Financials lanes in the
    [CRM Financials record](crm-hr-financials-ui-boundary.md).
