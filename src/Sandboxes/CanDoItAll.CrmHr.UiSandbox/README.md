# CRM / HR UI sandbox

Backend-free scenario host for the real CRM / HR surfaces from
[CanDoItAll.CrmHr.UI](../../UI/CanDoItAll.CrmHr.UI/README.md): the Home overview at
`/crm-hr`, the account summary with the activity history at `/crm-hr/account-activity` and
the Financials projection at `/crm-hr/financials`. It references only that rendering library
(and, through it, BaseLib, Common and the chart library with its Blazor-ApexCharts assets;
`Program.cs` registers the charts like the Web host). It has no database, no query service,
no Web, module, AgentFramework or navigation dependency. Every interaction updates
deterministic local state and the intent line; the production secondary tabs are host-owned
chrome and are represented by a labelled slot, not duplicated.

Run from the repository root with the SDK selected by `global.json` and the live Components
checkout. Open the browser at 1600x1000 for the matched desktop frame.

## Parity: production assets

```sh
npm ci --prefix Tailwind
npm run tailwind:build
npm run crmhr:watch:parity
```

Open http://127.0.0.1:5397/crm-hr. Parity links the real production CSS with its physical
ContentRoot plus the BaseLib CSS, fonts, icons and scoped component CSS. It requires the
production theme to exist; a missing theme fails the build explicitly. Equivalent direct
command:

```sh
dotnet watch --project src/Sandboxes/CanDoItAll.CrmHr.UiSandbox --launch-profile "CrmHr sandbox" --property:CrmHrAssetMode=Parity
```

## Fast: local assets and bounded scanning

```sh
npm run crmhr:css:build
npm run crmhr:watch:fast
```

Open http://127.0.0.1:5398/crm-hr. Fast generates the ignored `wwwroot/css/crmhr-fast.css`
from `Tailwind/crmhr-fast.css`, which scans only the rendering library and this sandbox. It
never writes the production theme and can build when that asset is absent. Both modes use
separate bin/obj directories; a contradictory runtime profile fails explicitly.

## Scenarios

`/crm-hr?scenario=<token>&layout=matched|narrow|flexible`. Unknown tokens fall back to
`populated` and `matched`. Controls replace the current history entry so a reload restores
the context; the intent line stays transient.

| Token | Presentation |
|---|---|
| `populated` | Totals above the preview caps, five directory rows, one sensitive row, three opportunities with and without amounts |
| `loading` | Loading phase: placeholder stat values, loading state, route buttons still usable |
| `empty` | A successful read with zero totals: the section-specific empty copy |
| `failed` | Failed phase with safe copy and Retry; Retry resolves to the populated overview |
| `sensitive` | Several sensitive rows; the sensitive card stays narrower than the directory rows |
| `long-text` | Markup-looking and very long names, summaries and titles rendered as text |
| `null-optionals` | Missing summaries, no sensitive rows, an opportunity without an amount and with an unknown owner |
| `large-totals` | Totals in the hundreds and thousands while the previews stay capped at 5 / 3 / 6 |

## Account summary and activity history

`/crm-hr/account-activity?scenario=<token>&layout=matched|narrow|flexible` renders the
account summary followed by the timeline in its three production compositions (CRM account
activity `crmhr-account-activity`, Directory party history `crmhr-directory-activity`,
Workforce history `crmhr-workforce-history`), each with its own wording and its own page
index over the same deterministic history (page size 10). The conversion intent updates the
local account to an active customer; the production page saves and reloads instead.

| Token | Presentation |
|---|---|
| `populated` | Prospect account with contacts, roles and counts; twelve entries over two pages with one overdue follow-up |
| `no-account` | The no-account empty state with its directory action; empty timelines |
| `active-customer` | An active customer: no conversion offer |
| `loading` | First read in flight: nothing accepted, counts shown as loading, paging disabled |
| `paging` | Another page loading over an accepted first page: totals kept, rows replaced by the loading state |
| `empty` | Zero entries accepted: each host's own empty copy, accepted zero counts and "No pages" |
| `overdue` | Seven follow-ups, four overdue: the overdue total and per-row Overdue badges |
| `long-text` | Markup-looking and very long names, summaries, titles and metadata rendered as text |
| `null-contacts` | No summary, no email, no phone, no roles, zero counts: the placeholder copy |
| `many-pages` | Forty-seven entries over five pages for independent paging per host |

## Financials

`/crm-hr/financials?scenario=<token>&layout=matched|narrow|flexible` renders the Financials
surface with the real chart. Retry resolves the failed scenario to the populated snapshot
locally; Monthly/Yearly re-project the same snapshot and issue no read.

| Token | Presentation |
|---|---|
| `populated` | Three currencies over two years in sparse months (no currency sells every month): the aligned category axis |
| `single-currency` | One currency over three consecutive months |
| `empty` | An accepted empty result: "No recognized sales", no chart, purchase and invoice figures unavailable |
| `incomplete` | The populated snapshot with three incomplete won records |
| `loading` | The loading phase |
| `failed` | The failed phase with Retry |
| `long-labels` | Eighteen consecutive months in two currencies with seven-figure amounts and twelve incomplete records |

All data is synthetic. No real person, organization, contact detail, confidential note or
financial record is present, and nothing here reaches an agent context.
