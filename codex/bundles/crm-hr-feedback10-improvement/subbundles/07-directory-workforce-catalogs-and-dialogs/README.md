# SB07 Directory And Workforce Catalogs And Dialogs

## Status

- `Completed`
- Implementation, focused regression, and inspected `1800x1100` normal/open-dialog proof pass.

## Objective

- Replace the permanent Directory and Workforce split editors with full-width, server-paged card catalogues patterned after the Agents catalogue, add opt-in bounded result scrolling, open the existing record details/editor workspaces in controlled dialogs, and make every CRM-HR workbench subpage title contextually clear.

## Covered Inputs

- Follow-up request items 1, 2, 3, and 6.
- Reopened `N005` / `R007`, `N010` / `R012`, plus new `R016` and `R017`.

## Prerequisites

- SB01 typed server-paged browser and the current Directory/Workforce query services remain trusted.
- Existing dialog, route-generation, stale-load, privacy, and lazy-tab behavior must be characterized before markup is moved.

## Exact Source References

- `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowser.razor`
- `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowser.razor.css`
- `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowserContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyRecordBrowser.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrWorkforcePage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Navigation/CrmHrRouteCatalog.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor.css`

## UI Composition Contract

- Primary surface: one full-width record-card catalogue per route.
- Supporting content: compact heading, result count, search, tag/scope controls, create/import actions, reset, and pager.
- Stats treatment: supporting badges only; no dashboard-card mosaic.
- List/editor organization: list stays on the page; independent record details/editing moves to one full dense dialog per selected record, matching the Agents interaction.
- Dialog sizing: full-size or wide based on the existing tabbed content; dialog body owns overflow while its header and footer remain usable.
- First viewport: search/actions and multiple rows of comparable cards are visible at `1800x1100`.
- Scroll owner: only the card-results region scrolls inside the page; picker-dialog consumers retain their existing default scroll behavior.
- Open-overlay proof: Directory and Workforce details/editor dialogs, nested party/contact/merge flows, close/reopen, and deep-link selection.

## Deliverables

- A strongly typed opt-in browser result-scroll mode whose default does not alter picker dialogs.
- Full-width Directory and Workforce catalogues using the existing source-paged browser.
- Route/deep-link synchronized controlled details/editor dialogs.
- `CRM Directory`, `CRM Workforce`, `CRM Recruiting`, `CRM Agents`, and `CRM Assignments` workbench labels, with concise secondary-tab labels unchanged.
- Updated component, navigation, freshness/privacy, and browser tests.

## Dependency Impact

- AppComponents remains domain-neutral.
- CRM-HR continues to adapt party queries through `PartyRecordBrowser`.
- No new project reference and no new feature partial are allowed.
- Failure reopens SB01 paging trust and blocks final closure.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-07`.

## Implementation Steps

1. Characterize current paging, default scroll behavior, route selection, and lazy tab loading.
2. Add a typed, default-off bounded-results mode to `PagedRecordBrowser` and forward it through `PartyRecordBrowser`.
3. Replace each `ListDetailShell` with a full-width catalogue and render the existing tabbed details/editor workspace inside a controlled dialog.
4. Preserve generation guards, explicit failures, privacy masking, deep-link behavior, nested overlays, and list context.
5. Update the typed route catalog workbench titles.
6. Run affected component/integration/browser tests and inspect normal/open-dialog screenshots at `1800x1100`.

## Do Not Do

- Do not copy the Agents in-memory loading model.
- Do not add a full-list fallback, client-side fake paging, Radzen, a raw table, new responsive application work, or a second page-local browser.
- Do not change secondary navigation labels to verbose prefixed forms.
- Do not introduce a stateful child component merely to move the existing page-owned orchestration.

## Acceptance Checklist

- [x] Directory and Workforce use the full available width for card browsing.
- [x] Filters and pager remain outside the opt-in bounded result-card scroll region.
- [x] Server paging, deterministic ordering, cancellation, loading, empty, failure, and retry behavior remain intact in focused component proof.
- [x] Selecting or deep-linking a record opens a controlled details/editor dialog.
- [x] Closing the dialog leaves a usable list and cannot be undone by a stale async completion.
- [x] Dialog tabs and content layer and scroll correctly while headers and action footers remain usable.
- [x] Every CRM-HR subpage workbench title is understandable outside module context in the typed route catalog and navigation tests.
- [x] Targeted tests and the affected module build pass.
- [x] Inspected large-screen normal/open-dialog browser proof passes.

## Proof Required

- Semantic positive: a multi-page Directory and Workforce data set browses, searches, scrolls, opens a record dialog, edits/saves, closes, and continues from a usable catalogue.
- Adversarial negative: the default picker-dialog browser does not gain a nested bounded scroll; a stale record load cannot reopen or overwrite a closed dialog; tab IDs remain route-based rather than title-based.
- Shallow-pass trap: CSS-only overflow on the whole page, paging labels without source paging, or wrapping the existing permanent detail pane in cosmetic chrome.
- Anti-stub audit: no fallback list, TODO, fixture-only branch, or hidden permanent editor remains.

## Progression Gate

- `CP-07` passed. Source behavior, the final focused regression selection, and the inspected populated Directory/Workforce normal and record-dialog states agree.

## Completion Record

- Shipped source: `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowser.razor`, `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowserContracts.cs`, `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`, `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrWorkforcePage.razor`, and `repo://src/Modules/CanDoItAll.Modules.CrmHr/Navigation/CrmHrRouteCatalog.cs`.
- Semantic positive proof: `repo://tests/Components/CanDoItAll.Tests.Components/CrmHrCatalogDialogTests.cs` proves Directory and Workforce deep links open controlled dialogs over bounded catalogues.
- Adversarial negative proof: `repo://tests/Components/CanDoItAll.Tests.Components/PagedRecordBrowserTests.cs` proves result scrolling is typed and opt-in; `repo://tests/Components/CanDoItAll.Tests.Components/CrmHrDirectoryPageFreshnessTests.cs` proves closing a dialog invalidates an in-flight refresh.
- Focused component result: the exact selection in `bundle://proof/SB07/browser-normal-and-dialog-review.md` completed with exit code `0`, `37 passed`, `0 failed`, `0 skipped` in `1m50s`.
- Affected build result: worker-reported CRM-HR module build completed with `0 errors`.
- Full-build result: the final Release solution build completed with exit code `0`, `0 errors`, `165 warnings` in `31.39s`; warnings include the existing `System.Security.Cryptography.Xml` `10.0.7` `NU1903` advisories. Exact command and test results are in `bundle://proof/final-validation.md`.
- Architecture/static audit: no `.csproj` changed; AppComponents remains domain-neutral; no new `partial class`, `IServiceProvider`, `BuildServiceProvider`, or direct `DbContext` path appears in the follow-up browser/API seams.
- Browser proof: `bundle://proof/SB07/browser-normal-and-dialog-review.md` records inspected `1800x1100` Directory and Workforce states, true bounded overflow, successful second-page navigation, Amina and Lucas dialogs, visible tab/content/action regions, byte lengths, and SHA-256 digests.
- Shallow-pass/adversarial result: measured full-width catalogues and actual page changes reject cosmetic paging; typed default-off scrolling and stale-close generation tests reject picker-scroll and stale-dialog regressions.
- Closure decision: `Completed`; `CP-07` passed.

## Reopen Triggers

- Nested scroll traps, list-context loss, query/dialog desynchronization, stale-load overwrite, privacy regression, ambiguous title, failing affected test/build, or clipped dialog actions.
