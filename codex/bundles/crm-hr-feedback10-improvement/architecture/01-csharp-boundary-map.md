# C# Boundary Map

## Target Projects And Ownership

| Project | Owns after implementation | Must not own |
| --- | --- | --- |
| `CanDoItAll.AppComponents` | Domain-neutral typed record option/request/page models, async loader delegate, browser body, picker-dialog host, selection/paging/loading/error mechanics. | CRM/HR enums/entities, EF Core queries, Projects types, shell routes. |
| `CanDoItAll.Modules.CrmHr` | Party/CRM filter enums, picker adapters/query services, contact wizard and persistence mapping, opportunity dialogs/pipeline, financial read projection, typed CRM route catalog. | Generic cross-domain paging mechanics, Projects data access, workbench state implementation. |
| `CanDoItAll.Modules.Projects` | Project query/filter/presentation mapping for the reusable picker. | CRM opportunity behavior or UI dialog state. |
| `CanDoItAll.Web` | Workbench descriptor tracking and shell composition. | Hard-coded CRM business queries or duplicate CRM page-title catalogs. |
| `CanDoItAll.Migrations.PostgreSql` | Generated contact-tag migration and current model snapshot. | Business logic or UI defaults. |
| Test projects | Direct isolated tests, integration/schema proof, and rendered behavior proof. | Production-only helper behavior used to make tests pass. |

## Target Top-Level Types

Names may adjust to repository convention, but responsibilities may not collapse:

- AppComponents:
  - `RecordBrowserRequest<TFilter>`
  - `RecordBrowserPage<TKey>`
  - `RecordBrowserOption<TKey>`
  - `RecordBrowserLoader<TKey, TFilter>` delegate
  - `PagedRecordBrowser<TKey, TFilter>`
  - `PagedRecordPickerDialog<TKey, TFilter>`
- CRM/HR:
  - `CrmHrRecordPickerQueryService`
  - typed party/record filter records or enums
  - `ContactMethodWizardDialog` with a small step enum and isolated draft
  - opportunity create/detail/edit dialog components and reusable pipeline presentation
  - `CrmFinancialSnapshotQueryService`
  - typed financial availability/series records
  - typed static `CrmHrRouteCatalog` (or equivalent) consumed by CRM secondary tabs and Web workbench descriptor resolution
- Projects:
  - `ProjectRecordPickerQueryService` or equivalent cohesive typed query.

Do not create all listed abstractions merely to match names. Preserve the separation with the smallest clear types.

## Contracts Versus Implementations

- Shared browser contracts contain UI-neutral page/search/tag/filter data and a typed loader delegate.
- Domain filters remain module-owned and are supplied as the loader's generic filter argument.
- The delegate is the test/composition seam; do not add an interface with one trivial implementation unless DI or an independent lifecycle makes it necessary.
- EF implementations live in CRM/HR or Projects and return neutral browser pages only at their UI adapter boundary.
- Financial availability is a typed record/enum, not display-string or nullable-decimal inference.
- Workbench title resolution uses a typed CRM route catalog because the existing shell-menu contributor flattens entries into main navigation. Do not misuse it as a metadata-only extension.

## Composition Root Responsibilities

- `CrmHrModuleServiceCollectionExtensions` registers top-level CRM record and financial query services.
- Projects module registration owns any project picker query service.
- Razor hosts provide strongly typed loader delegates to the generic browser/dialog.
- Web composition resolves CRM subroute metadata from the typed catalog before its existing generic `ShellNavigation.MatchRoute` fallback.
- No domain service resolves dependencies through `IServiceProvider`.

## Old Responsibilities To Remove Or Leave

| Existing owner | Remove/move | Leave |
| --- | --- | --- |
| `PartyPicker.razor` | Full-options dropdown, inline full-form fallback, domain-independent browsing mechanics. | At most a thin CRM adapter/host if existing call sites benefit. |
| `PagedCardGrid.razor` | Any claim that it provides data-scale paging. | Small already-loaded card collections. |
| `CrmHrDirectoryPage.razor` | Comma tag parsing, contact-wizard internals, query/paging rules. | Route/load orchestration, selected party, dialog open/close, notifications. |
| `CrmHrCrmPage.razor` | Opportunity filter predicates, editor markup, financial aggregation, project search. | Selected account/tab/dialog orchestration and route reconciliation. |
| `CrmHrServices.cs` | New picker and financial responsibilities. | Existing writes and behavior not required to move for this request. |
| `ResourceCardPicker.razor` | Hidden in-memory-only assumption if evolved. | Existing typed card presentation and compatible consumers where honest. |

## Temporary Bridges And Removal Plan

- A thin `PartyPicker` compatibility host is allowed only if it delegates to the new browser and requires a loader; an old `Options` path must not remain as a silent fallback. Remove obsolete parameters and update all consumers in SB01/SB02.
- Existing `ResourceCardPicker` consumers may remain on their current bounded lists. If new components are added instead of a breaking evolution, share presentation records/helpers deliberately and do not duplicate filtering rules.
- Contact-tag migration uses default `[]` to bridge existing rows. No long-lived dual read/write format is allowed.
- Opportunity stacked editor may exist only until SB04 closes; final source assertions must show it is no longer permanently rendered.

## Boundary Acceptance

- Shared UI compiles without CRM/HR or Projects references.
- New query/projection types instantiate directly in tests without constructing `CrmService` or a Razor page.
- The large services/pages shrink or remain thin orchestrators relative to the responsibilities moved; line count alone is supportive, not sufficient.
- Adding another record kind or picker consumer does not require editing the shared component's domain logic.
- No new partial file is part of the final design.

## Follow-Up Boundary Addendum

| Owner | New follow-up responsibility | Explicit exclusion |
| --- | --- | --- |
| `CanDoItAll.AppComponents` | Domain-neutral, strongly typed opt-in results-scroll presentation for the existing paged browser. | CRM card semantics, routes, dialog state, or paging queries. |
| `CanDoItAll.Modules.CrmHr` | Full-width Directory/Workforce card composition, page-owned dialog/load state, existing application commands and bounded projections. | HTTP transport, JWT composition, direct scenario bootstrapping. |
| `CanDoItAll.Web` | `/api/crm-hr` route binding, typed request binding, cancellation, and result/status mapping through existing application services. | CRM-HR validation duplication, EF entities/`DbContext`, startup seed records. |
| Repo/active Codex skill | Operator workflow and exact public API contract. | Database access, secrets, or production runtime behavior. |
| External seed operation | Search-before-create orchestration through the live HTTP contract. | Committed seed hook, direct SQL/EF, destructive cleanup. |

- Directory and Workforce keep generation-guarded orchestration in their current page owners. Moving the existing tabbed markup into a controlled dialog is a presentation-boundary change, not a new business layer.
- One full details/editor dialog per selected record matches the Agents interaction and avoids duplicating read-only/edit orchestration. Nested contact, party-picker, merge, and delivery-unit dialogs remain independent controlled overlays.
- API endpoints use existing concrete application services directly. Adding one-implementation interfaces or a generic repository only for Minimal API handlers is rejected.
