# C# Testability Plan

## Characterization Tests

- Capture current `ResourceCardPicker` typed selection/favorite/search behavior before evolving or extracting shared presentation.
- Capture current party assignment/allocation selection behavior before changing `PartyPicker`.
- Capture current Directory save/load mapping and contact persistence before adding contact tags.
- Capture current opportunity save, selection route, stage advance, conversion, and linked-project behavior before dialog extraction.
- Capture current shell route matching for `/crm-hr/*` resolving to `CRM / HR` so the contextual-title change is demonstrated, not assumed.

## Isolated Unit And Component Tests

### Shared record browser

- Fake loader receives exact normalized request and cancellation token.
- Page changes request the correct index; filter changes reset to zero.
- Typed selection returns `TKey`, not a string/boxed id.
- Loading, empty, failure, retry, and stale response states are deterministic.
- Dialog focus/cancel/select behavior does not require CRM/HR or a database.

### CRM/HR and Projects adapters

- Seeded query tests verify stable `Skip/Take`, total count, case-insensitive search, conjunctive tag filters, and typed party scope.
- Project adapter maps project-list identity/status/search without depending on CRM/HR.
- Sensitive records do not become more visible than under current rules.

### Contact wizard

- Direct step-state tests cover initial type step, validation, Back, Cancel, Finish, and one-time commit.
- Component regression performs Add -> choose/default type -> Next without value -> Cancel/Remove and proves no exception/no model mutation.
- Mapping tests prove contact tags normalize and round-trip.

### Opportunity dialogs

- Pipeline/filter model tests prove search/filter semantics and compact controls use typed values.
- Create wizard, detail, and edit components instantiate directly without `CrmHrCrmPage`.
- Cancel does not mutate selected opportunity; save returns/selects the new id; project and party pickers propagate typed ids.

### Financial projection

- Direct service tests cover won sold totals, non-won exclusion, currency separation, month/year ordering, empty state, bought unavailable, invoices unavailable, and cancellation/error propagation.
- Rendering tests prove unavailable is not displayed as numeric zero and charts receive only valid series.

### Shell titles

- Route-catalog/workbench descriptor tests cover each CRM/HR route, unchanged main navigation, and non-CRM controls.
- Workbench service tests cover stable tab/restore identity and multiple simultaneous CRM/HR tabs.

## Adversarial Negative Tests

- A 1,001-record data set where the desired result exists only after the first page; client-side first-page filtering must fail.
- Two records share a display name; stable id tiebreaking prevents duplicates or skipped records between pages.
- A slow old search completes after a fast new search; old results must not overwrite new.
- Two selected tags where records match only one; conjunctive semantics exclude them.
- Contact wizard Cancel after entering data; no contact or tags persist.
- Remove an existing contact/address/relationship after list reorder; stable identity removes or updates the intended item rather than the loop's terminal index.
- Opportunity editor Cancel after changing owner/project; persisted model remains unchanged.
- Mixed USD/EUR won opportunities; the service and opportunity pipeline must not emit one summed or unlabeled amount.
- A won opportunity without a Won stage-history transition or amount produces an incomplete-data count/state, not an `UpdatedAtUtc` fallback or zero.
- No invoice/purchase records; UI must say unavailable rather than `0`.
- `/crm-hr/directory` and `/crm-hr/crm` open together; titles and restore keys remain distinct.

## Integration And Composition Smoke

- DI resolves new CRM/HR and Projects query services from normal module registration.
- `CanDoItAll.slnx` builds with AppComponents/CRM/HR/Projects/Web direction intact.
- PostgreSQL migration adds contact tags with `[]` default and existing rows load.
- Directory save/load/import/export/merge and source snapshot paths remain valid.
- CRM opportunity create/edit/link project reloads through production services.
- Browser flows exercise actual dialogs and shell tab bar at `1800x1100`.

## Fake/Stub Policy

- Fakes are permitted only at unit/component boundaries (loader delegate, clock, deterministic DB fixture).
- No production fallback loader, fixture-specific branch, TODO, `NotImplementedException`, fake chart data, or seeded-only behavior may close a subbundle.
- Unit tests must instantiate extracted behavior without constructing `CrmService`, `PartyDirectoryService`, or the large Razor pages unless the test is explicitly an integration/composition smoke.

## Proof That Separation Is Real

- Shared-browser tests reference no CRM/HR or Projects type.
- Query/projection tests call new types directly.
- Source audit finds no new partial/nested service and no domain logic in AppComponents.
- CRM/Directory pages no longer own the moved filtering/wizard/aggregation logic.
- `PartyPicker` is removed or demonstrably thin.
- Downstream Playwright checks prove the composition root uses the extracted behavior.

## Follow-Up Test Plan

### Catalogue and dialog behavior

- `PagedRecordBrowser` preserves its existing default class/scroll behavior and adds the bounded-results modifier only when explicitly requested.
- Directory and Workforce issue source-paged requests after search/filter/page changes, render several comparable cards, and keep the pager outside the result scroll owner.
- Selection and `partyId` deep links open the full dialog; close invalidates pending loads and returns to a usable list without changing the route-based workbench identity.
- Existing privacy masking and lazy Activity/Relations/History loading remain covered after markup relocation.
- Nested CRM-HR dialogs remain focusable and their action footers remain visible at `1800x1100`.

### HTTP API

- API tests use the normal mapped `/api/crm-hr` group and prove create/read/update across party, workforce, skills/capacity, and recruiting.
- Invalid model and nonexistent reference cases return the shared structured error shape and do not partially persist.
- List/search operations enforce bounded page sizes and return safe projections; no confidential notes or private contact values appear in catalogue responses.
- Source assertions reject `DbContext`, entity construction, TODO/`NotImplemented`, and seed-only routes in Web API code.

### Scenario operation

- First HTTP run creates deterministic parties, delivery units, skill/capacity, and multiple recruiting stages.
- Second HTTP run finds the same external codes and updates/reuses their ids rather than creating duplicates.
- Browser proof cross-checks Directory, Workforce, Recruiting, record dialogs, scrolling, paging, and contextual workbench titles.
