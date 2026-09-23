# CRM Financials UI boundary

Execution record of the third bounded CRM / HR extraction on `components-decoupling`: the CRM
Financials panel (recognized sold value per currency, the monthly/yearly chart, the incomplete
record note and the explicitly unavailable purchase, invoice and distribution figures) now
renders from `src/UI/CanDoItAll.CrmHr.UI`, the module keeps the public `CrmFinancialsPanel`
host with its `AccountPartyId` entry, and the financial query owner and its recognition rules
are unchanged. Only this panel moved; the CRM / HR module is not declared decoupled. The
earlier extractions are recorded in [CRM / HR Home UI boundary](crm-hr-home-ui-boundary.md)
and [CRM / HR account summary and activity history UI boundary](crm-hr-account-activity-ui-boundary.md).

## Ownership

| Concern | Owner |
|---|---|
| Rendering: phases, currency metrics, unavailable sources, incomplete note, period toggle, the real bar chart, empty-sales state | `CanDoItAll.CrmHr.UI/Financials/CrmHrFinancialsSurface` |
| Presentation records and intents | `CrmHrFinancialsPresentation`, `CrmHrFinancialsSnapshot`, `CrmHrCurrencyTotal`, `CrmHrFinancialPeriodAmount`, `CrmHrFinancialsIntent` (`Retry`, `SetPeriod`), `CrmHrFinancialsText` (rendering library) |
| Chart projection (series per currency aligned by period key, per-instance options) | `CrmHrFinancialsChart` (rendering library) |
| Read lifecycle: account identity, one accepted snapshot per read, Loading/Ready/Failed, retry admission, period projection, fencing of late outcomes, wrong-account rejection | `CrmFinancialsReadSession` (module) |
| Projection of the query owner's snapshot into the presentation record | `CrmFinancialsPresentationMapper` (module) |
| Public host under the existing name, tab placement and `AccountPartyId` entry | `CrmFinancialsPanel` (module), composed by `CrmHrCrmPage` in the Financials tab, unchanged |
| Recognition semantics and the query | `ICrmFinancialSnapshotQueryService` / `CrmFinancialSnapshotQueryService`, unchanged |

### Contract decision

The presentation records stay private to the rendering library and its hosts, as for Home and
the account/activity surfaces. No module type crosses the boundary: the mapper copies totals,
monthly and yearly buckets, the incomplete count and the three availabilities into
`CrmHrFinancialsSnapshot`; `FinancialDataAvailability` becomes the library's own
`CrmHrFinancialAvailability`. Nothing is aggregated, converted or recomputed in the library:
the recognized amount and currency of the first Won transition, the exclusion and count of
incomplete records, separate per-currency totals and the UTC period grouping are the query
owner's results and are rendered as delivered. Bought, overdue-invoice and sold-versus-bought
figures render as "Unavailable" from the owner's availability flags; an accepted empty result
renders the "No recognized sales" copy without a chart and without a fabricated figure.

### Chart boundary and assets

The chart is the real `CdaChart` from `CanDoItAll.Components.Charts` (the same component the
module rendered before). The rendering library gained one direct reference for it,
`CanDoItAll.Components.Charts`; through the repository's `Directory.Build.targets` that
package reference resolves to the live sibling project
`../CanDoItAll.Components/src/CanDoItAll.Components.Charts` (checkout `7b618cda`), which
declares `Blazor-ApexCharts 6.1.0` and `Microsoft.AspNetCore.Components.Web`. The evaluated
compile graph of the library is therefore BaseLib, Common, Charts and Blazor-ApexCharts on top
of the framework; the library's assembly references Charts directly and never Blazor-ApexCharts
(guarded by `CrmHrFinancialsUiBoundaryTests`). Blazor-ApexCharts serves its script as a static
web asset and imports it as a JavaScript module from the chart component; no script tag is
added to the Web host or the sandbox, and the sandbox registers `AddCanDoItAllCharts()` like
the Web host. Both sandbox asset modes reach the chart the same way; Parity was used for the
observations below.

Series alignment: ApexCharts positions the n-th point of every series under the n-th category
of a category axis, so sparse currencies would misalign. `CrmHrFinancialsChart.BuildSoldSeries`
therefore builds one chronological category list from every period any currency recognized a
sale in and gives each currency one point per category, a zero-height placeholder where that
currency has no sale. The placeholder is presentation alignment only: it is never a figure, it
is not summed anywhere and the metric totals come from the snapshot (guarded by
`CrmHrFinancialsChartTests`). Month labels are English interface words under every server
culture (`Jan 2025`, correction C1 below); years are rendered as four invariant digits.

## Behavior matrix

Classification: **Preserve** = current behavior kept; **Safeguard** = authorized lifecycle
change covered by tests; **Correction** = explicit observable change recorded here.

| Behavior | Before → after | Class | Test |
|---|---|---|---|
| Sold totals per currency (`N2` amount + code), `Sold` = "No recognized sales" when none; Bought and Overdue invoices "Unavailable"; incomplete count | same values, now from the presentation record | Preserve | mapper `Maps_totals_buckets…`, surface `Ready_snapshot_renders…`, `An_accepted_empty_sales_result…`, host `Renders_currency_safe_metrics…` |
| Incomplete note (`N incomplete`) only when the count is positive | same | Preserve | surface, sandbox `incomplete` |
| Recognized sold chart: bar type, category axis, legend at the bottom, no toolbar or zoom, precision 2, Calm palette, height 340, description text | same options, created per surface instance instead of one static instance | Safeguard | chart `An_empty_snapshot_yields_no_series_and_options_are_never_shared`, surface `Two_surfaces_never_share_chart_options`, browser lane |
| Sparse multi-currency periods aligned by period key, not by positional index | positional (each series carried only its own periods) | Correction | chart `Sparse_currencies_share_one_chronological_category_axis…`, sandbox `Populated_scenario_plots_one_aligned_series…`, browser lane (three monthly categories, two yearly) |
| Monthly/yearly toggle re-projects the accepted snapshot locally; no read | same | Preserve | session `Period_switches_are_local…`, host `Retry_reads…period_choice_survives…`, browser lane |
| A new instance starts Monthly; the same instance keeps its period across account changes | same policy | Preserve | session `Period_switches_are_local_and_survive_an_account_change…`, host |
| Loading, generic failure copy without exception details, Retry | same copy; Retry is admitted only for the failed current account and generation, duplicate admissions ignored | Safeguard | session `Retry_reads_the_failed_account_again…`, host `Failed_load_shows_retryable_generic_error…`, surface `Failed_phase_offers_a_retry…` |
| Same-account echo issues no read | same | Preserve | session `A_same_account_echo_issues_no_read…`, host `Same_account_rerender_issues_no_read…` |
| Account change while a read is pending: the previous read is retired, its late success or failure is inert, A → B → A does not revive the first outcome | success was fenced by operation reference, but the generic catch and finally compared only the reference and disposal cancelled without replacing the operation, so a late thrown outcome could still enter them | Safeguard | session `Another_account_retires_the_pending_read…`, `A_late_failure_of_a_retired_read…`, host `…an_account_change_retires_the_pending_read` |
| A snapshot answering with another account is rejected as a failure | not checked | Safeguard | session `A_snapshot_for_another_account_is_never_accepted` |
| `Guid.Empty` fails at once without a query; an unknown account fails through the owner's exception with the generic copy | the owner's `ArgumentException` reached the generic catch | Safeguard | session `An_empty_account_identifier_fails_without_a_read`, browser lane (unknown-account path is the owner's `KeyNotFoundException`, integration facts) |
| Disposal ignores late outcomes; token sources are disposed only after their read returned | disposal disposed the operation the read still held | Safeguard | session `Disposal_makes_late_outcomes_inert…` |
| Month categories are English (`Jan 2025`) whatever the server culture is; the amounts keep the ambient number format | the ambient culture's short month name (`led 2025` on a Czech server) | Correction (C1) | unit `CrmHrPresentationCultureTests` (cs-CZ, ja-JP, ar-SA, el-GR, en-US), surface `The_rendered_chart_categories_are_english…`, browser lane (exact labels) |
| Test identifiers `crmhr-financials-panel` (now with `data-phase`, `data-account-id`, `data-period`), `-metrics`, `-incomplete`, `-month`, `-year`, `-sold-chart`, `-retry`, `-distribution-unavailable`, `-invoices-unavailable` kept; `-loading`, `-failed`, `-sold-empty` added | additive | Preserve | surface and browser lanes |

No exchange rate, combined total, purchase figure, invoice status, new query or new route was
added.

## Consumers

- `CrmHrCrmPage` (`/crm-hr/crm`) composes `CrmFinancialsPanel AccountPartyId=…` in the
  Financials tab of the account dialog, unchanged.
- The sandbox specimen `/crm-hr/financials` composes `CrmHrFinancialsSurface` directly with
  deterministic snapshots.

## Validation

Commands run from the repository root in Release with `/m:1`; discovery counts were stated
before execution. See the execution record for the run sequence and the final numbers.

| Slice | Filter | Expected / discovered | Provider |
|---|---|---|---|
| Read session, mapper, chart projection | `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmFinancials\|…CrmHrFinancials` | 18 / 18 (10 session + 3 mapper facts + 3 availability cases + 3 chart) | scripted query, pure |
| Surface, host, sandbox, boundary | `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmFinancials\|…CrmHrFinancials` | 23 / 23 (5 surface + 4 host + 7 sandbox theory cases + 3 sandbox facts + 4 boundary) | bUnit, scripted query, real chart component under loose JS interop |
| Query owner semantics | `FullyQualifiedName~CanDoItAll.Tests.Integration.CrmHr.CrmFinancialSnapshotQueryIntegrationTests` | 2 / 2 | EF Core through `TestApplication` |
| Production browser lane | `FullyQualifiedName~CrmHrFinancialsBrowserTests` | 1 / 1 | real Web host, PostgreSQL, seeded through the application services and the persistence model |

## Readiness

| Dimension | State | Evidence |
|---|---|---|
| Semantic ownership | Proven | `CrmFinancialsReadSession` (10 facts) owns the read, the accepted snapshot, retry, period and late-outcome fencing; the query owner is unchanged (2 integration facts) |
| Deterministic rendering | Proven | the surface renders from explicit records with no service (5 facts) and in the sandbox (10 cases) |
| Scenario interactions | Proven | sandbox scenarios listed in its README; Retry and period actions update local state only |
| Lightweight compile graph | Proven for the direct reference set, stated for the transitive graph | `CrmHrFinancialsUiBoundaryTests` guards the surface parameters, the absence of `[Inject]`, the direct references (BaseLib, Common, Charts, Components.Web) and the composed chart in both hosts; the transitive graph (Blazor-ApexCharts through Charts) is recorded above, not asserted recursively |
| Browser sandbox | see execution record | in-app browser probes of `/crm-hr/financials` |
| Production route | see execution record | real host and PostgreSQL: seeded sparse multi-currency sales, plotted geometry, legend, monthly/yearly categories, constrained width, account switch to an empty account |

## Open items

- The chart's JavaScript is served by the Blazor-ApexCharts static web assets and imported by
  the chart component at first render; a browser without that module shows the chart shell
  without plot geometry. The browser lane asserts the plotted geometry.
- No development-loop timing was measured for this extraction: the surface was added to the
  existing rendering library rather than to a new project graph, so the earlier Home
  measurements stay separate and no new hot-reload claim is made.

## Execution record (2026-09-17)

- Start: `de7934f3a2b77048a33b244c800406c43e3a9486` on `components-decoupling`, clean tree,
  after the R1–R3 repairs of the same continuation (recorded in the account/activity record
  and committed first as the verified checkpoint). Nothing was pushed, merged, rebased or
  reset; no signing or permission configuration was touched.
- Source: `Financials/` in the rendering library (presentation records, chart projection,
  surface), the library's `CanDoItAll.Components.Charts` reference, `CrmFinancialsReadSession`,
  `CrmFinancialsPresentationMapper`, the rewritten `CrmFinancialsPanel` host (`IDisposable`
  instead of `IAsyncDisposable`, same public entry), the sandbox fixture, specimen and
  `AddCanDoItAllCharts()` registration, and the `_Imports` entries. `CrmHrCrmPage` composes the
  panel unchanged; the query owner is unchanged.
- Sandbox observation in the desktop app browser (Debug, Parity assets; computed-style
  probes because the hidden pane does not paint): `populated` at the matched 1600×1000 frame
  (1419 px content width): phase ready, three currency metrics, purchase and invoice figures
  "Unavailable", chart state `ready` with an SVG canvas of 340 px, 18 bars for 3 currencies ×
  6 categories of which 7 have visible geometry (the 7 seeded sales), legend EUR / GBP / USD,
  6 chronological month categories, no document, panel or chart overflow. Clicking Yearly
  logged `Period: Year`, the title became "Sold value by year", the categories 2025 / 2026 and
  5 bars had visible geometry. `long-labels` at the constrained 960 px frame: 36 bars over 18
  categories all visible, legend EUR / USD, "12 incomplete", no overflow of the document, the
  panel, the metric grid or the chart. `empty`: no chart element, "No recognized sales",
  "Unavailable" purchase and invoice figures, no percentage anywhere. No console error from
  these loads (the only console errors in the pane were connection retries logged before the
  sandbox came up).
- Validation (Release, `/m:1`, counts stated before execution and matched; the Unit and
  Components solutions were rebuilt and rerun after the last test corrections):
  - Unit `FullyQualifiedName~CanDoItAll.Tests.Unit.CrmHr.CrmFinancials|…CrmHrFinancials`
    18 / 18 passed. A first run failed one chart fact whose expected month labels were
    hard-coded in English while the machine renders the module's culture-dependent short
    month names (`led 2025`); the expectation now uses the same format.
  - Components `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.CrmFinancials|…CrmHrFinancials`
    23 / 23 passed. The first run failed the same culture expectation in the surface and
    sandbox facts and one host fact that asserted the period render synchronously while the
    retry click's own completion was still queued on the dispatcher; the fact now awaits it.
  - The whole CRM / HR Components topic (`FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.`)
    199 / 199 passed (`ComponentTestHarness` on PostgreSQL for the page facts).
  - Integration `FullyQualifiedName~CanDoItAll.Tests.Integration.CrmHr.CrmFinancialSnapshotQueryIntegrationTests`
    2 / 2 passed (EF Core through `TestApplication`; the recognition semantics the extraction
    must not change: first Won transition, separate currencies, incomplete rows, empty and
    unknown accounts).
  - Browser lane `FullyQualifiedName~CrmHrFinancialsBrowserTests` 1 / 1 passed (10 s) on the
    real Web host and PostgreSQL (seeded through `PartyDirectoryService` and the persistence
    model: three Won transitions in EUR / USD across 2025 and 2026 plus one incomplete Won
    record, and a second account without opportunities). Two earlier runs failed on test
    mechanics only: the axis-label probe read a label's `<text>` including its accessible
    `<title>` child and so doubled every label (the probe now reads the visible `<tspan>`), and
    the failed-resource check counted the circuit's own connection request aborted by the
    navigation to the second account (it now ignores `/_blazor` requests and `ERR_ABORTED`).
    Three captures (monthly and yearly at 1600×1000, the yearly view at 1100×900) are in the
    git-ignored Playwright output folder (crm-hr-financials); the 1600×1000 monthly capture
    shows the three plotted bars under their three month categories with the EUR and USD
    legend, the currency metrics, the "1 incomplete" note and the unavailable figures. Browser lanes rerun in the same
    pass: `CrmHrAccountActivityBrowserTests` 2 / 2 (now also asserting the accepted follow-up
    counts on the CRM card and the accepted zeros of an account without interactions),
    `CrmHrHomeBrowserTests` 1 / 1, `CrmHrSensitiveDataFlowTests` 1 / 1 (the Directory activity
    tab on the new history lane).
- Static gates: the portability tooling self-tests passed (6 + 4). Portability-static ran on
  the complete tree without `--tracked-only`, the new untracked files included. The only
  delta was one `case-policy` finding in `CrmHrFinancialsSandboxFixture.cs`: the scenario
  token parsing with `OrdinalIgnoreCase`, the same reviewed pattern the other sandbox
  fixtures carry in the baseline. The baseline was refreshed for that single entry, its diff
  inspected (one added entry, count 14683 → 14684), and the final enforcement without
  `--write-baseline` on the final tree reports `PASS (14684 reviewed executable-source
  findings unchanged)`. `Test-Documentation.ps1` passed after the final update of this
  record.
- Not run: the broad Stable gate. The rendering library gained a package reference that the
  repository already resolves for the module (no new project, solution entry,
  `Directory.Build.*`, shared persistence, migration or test-infrastructure change), so no
  named trigger arose. The historical `SecretScanningTests` failure on ignored retained
  artifacts and the load-sensitive canvas preview fact recorded in the Home record are
  unchanged and not cleared by this pass.
- Measurements: none taken. The surface was added to the existing rendering library rather
  than to a new project graph; the Home measurements stay separate and no hot-reload claim is
  derived from this move.
- Tree: the R1–R3 repairs are the first signed commit of the continuation (the verified
  checkpoint), the Financials extraction with this record, the READMEs and the testing entry
  the second; nothing outside the two commits was modified.

## Corrections after the `da50a040` review (2026-09-17)

- **C1, generated interface language.** `CrmHrFinancialsText.PeriodLabel` formatted the month
  with the ambient culture, so a Czech server rendered `led 2025` and the tests had been
  aligned to the machine. The repository has no application-wide presentation-language
  policy (the Web host only uses the invariant culture for machine formats), so the rendering
  library now owns the smallest feature policy, `CrmHrPresentationCulture`: month labels are
  formatted with an explicit English culture, years as four invariant digits, and the
  activity timestamp keeps the ambient short pattern with English AM/PM designators. The
  audit of the remaining CRM / HR surface found no other word-producing format: the module
  uses explicit `yyyy-MM-dd` / `yyyy-MM-dd HH:mm` machine formats, and amounts and counts are
  numeric (`N0`, `N2`, `0.##`) and stay with the ambient number format. The process culture,
  input parsing, UTC recognition buckets, decimals and the time-zone policy are unchanged.
  Regressions: `CrmHrPresentationCultureTests` (9 cases: the twelve English month labels
  written out by hand under `cs-CZ`, `ja-JP`, `ar-SA`, `el-GR` and `en-US`, chart categories
  under `cs-CZ`, the timestamp pattern under `cs-CZ`, `en-US` and the 12-hour `el-GR`, and
  proof that the ambient culture is restored and never changed), the surface theory
  `The_rendered_chart_categories_are_english_under_a_non_english_server_culture` (`cs-CZ`,
  `ja-JP`), and literal English expectations in the chart, surface and sandbox facts that had
  used the formatter under test as their own oracle.
- **C2, browser failure oracle.** The lane ignored every `/_blazor` URL and every
  `ERR_ABORTED`. `CrmHrBrowserOracle` now records page errors, console errors, every failed
  request (assets, API and circuit traffic alike), every HTTP response with status 400 or
  above, the Blazor error UI and the reconnect dialog. The single allowance is the teardown the
  journey causes itself: the `POST /_blazor/disconnect` beacon aborted with
  `net::ERR_ABORTED` while the journey's own `NavigateAsync` replaces the document, at most
  one per replaced document; it is written to `expected-teardown.txt` next to the captures
  (one entry in the recorded run). The chart assertions now use the seeded oracle: the exact
  English categories `Jan 2025 | Mar 2025 | Feb 2026`, the legend `EUR`, `USD`, every bar's
  series, category index and plotted value (12000, 3200.25 and 2500 with zero-height
  placeholders elsewhere), bar heights in the proportion of their values (2 % tolerance),
  left-to-right category order, the settled yearly projection (`2025 | 2026` with 12000,
  3200.25 and 2500), the settled return to monthly, the metric totals by digits
  (15200.25 EUR, 2500.00 USD) and three plotted bars at the constrained width.
- Validation of the corrections (Release, `/m:1`, discovery stated before execution): Unit
  `…CrmHrPresentationCultureTests` 9 / 9, Unit Financials 18 / 18, Unit activity 21 / 21,
  Components Financials 25 / 25 (23 + the two culture theory cases), Components account and
  activity 38 / 38, browser lane `CrmHrFinancialsBrowserTests` 1 / 1 on the real Web host and
  PostgreSQL. The first browser run of the strengthened lane failed one geometry assertion
  that compared the x position of zero-height placeholders (their empty box reports x = 0);
  the order check now covers plotted bars only. The 1600×1000 monthly capture was inspected:
  English month labels under the three plotted bars, `15 200,25 EUR` and `2 500,00 USD`
  totals in the host's number format.
