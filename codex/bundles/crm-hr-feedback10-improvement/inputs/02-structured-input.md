# Structured Input

## Core Objective

- Turn the CRM/HR feedback into scalable, predictable large-desktop workflows that remain usable beyond small demo datasets and do not expand the current service/page monoliths.

## Success Criteria

- Every mutable CRM/HR tag surface uses BaseLib `TagEditor`; no comma-delimited tag editor remains.
- Adding and immediately removing/cancelling an empty contact never throws or persists a blank contact.
- Contact creation is a two-step dialog wizard: typed contact-method cards, then value/label/tags and relevant metadata.
- A reusable, strongly typed record browser/picker pages data at the query boundary, searches, filters by tags and record/party type, supports people and organizations, and is reused for standalone lists.
- Opportunity browsing is a compact reusable pipeline/list with at most two filter rows; owner and party selection use the scalable picker.
- Opportunity create, view, and edit are independent dialog flows; related projects use a reusable project picker.
- A Financials tab next to Overview shows honest opportunity-derived sold analytics, explicit unavailable placeholders for purchase/invoice data, month/year bars, and sold/bought distribution without fabricated values.
- CRM/HR subroutes create distinguishable workbench tab titles.
- All six Behavioral subbundles pass realistic positive and adversarial negative proof, targeted tests, build, and applicable large-screen browser review.

## Hard Constraints

- Reuse BaseLib wrappers and the existing Tailwind theme. Do not introduce Radzen.
- Prefer shared components and top-level services over raw structural markup or new page-local helper systems.
- Keep identifiers, filters, query contracts, workflow steps, and availability states strongly typed.
- Fail predictably and render explicit loading/error states; do not add silent fallbacks to unpaged dropdowns, stale data, or fake financial values.
- Application pages are large-desktop-only in this bundle. Do not add or tune small/medium/tablet/mobile behavior.
- Preserve all raw source artifacts byte-for-byte.
- No XML documentation comments are required.

## Allowed Side Effects

- Production, test, package-reference, and PostgreSQL migration changes explicitly owned by SB01-SB06.
- A one-way CRM/HR module reference to `CanDoItAll.AppComponents` and a Charts package reference are allowed only after the dependency checkpoint passes.
- Contact-point tag persistence may add a backward-compatible `TagsJson` column with default `[]`.
- None beyond documented subbundles.

## Source Artifacts

- `bundle://inputs/feedback10.docx`
- `bundle://inputs/03-feedback10-extracted.md`
- `bundle://inputs/feedback10-rendered-pages/page-1.png`
- `bundle://inputs/feedback10-rendered-pages/page-2.png`
- `bundle://inputs/feedback10-rendered-pages/page-3.png`
- `bundle://inputs/feedback10-media/`

## Input Coverage Signals

- `N001`: informational `CRM:` heading; explicitly close as N/A rather than inventing work.
- `N002`: global CRM/HR tag-editor consistency.
- `N003`: two separate outcomes—empty-row crash prevention and dialog wizard creation.
- `N004`: scalable paging/search/tag/type-filter component and cross-form CRM/HR use.
- `N005`: reuse the same browsing surface for ordinary searchable lists.
- `N006`: reusable opportunity pipeline, compact filters, scalable owner selection.
- `N007`: opportunity create wizard plus list/detail/edit dialogs.
- `N008`: reusable related-project selection.
- `N009`: Financials tab, metric availability semantics, time charts, and distribution chart.
- `N010`: contextual workbench tab titles, not merely browser `<PageTitle>` text.

## Dependency And Sequencing Signals

- SB01 is the critical shared picker/query/UI boundary foundation.
- SB02 depends on SB01 and proves cross-form picker/list reuse plus tag consistency.
- SB03 depends on SB02 because the contact/relationship flows consume the shared picker and TagEditor contract.
- SB04 depends on SB02 and SB03; it reuses the picker, dialog state conventions, and stable draft-removal behavior.
- SB05 depends on SB04. Although its projection service could be developed separately, it shares `CrmHrCrmPage.razor`; sequential ownership avoids unsafe merge and UI-state conflicts.
- SB06 follows all earlier work and owns contextual titles plus final regression/hardening.

## Validation Expectations

- Use `Behavioral` proof for SB01-SB06. Do not downgrade to Standard after implementation and do not add Governed manifests or ceremony.
- Every subbundle records the literal raw note, shipped behavior, changed source, exact tests/commands, shallow-pass trap, adversarial negative case, realistic positive case, and anti-stub audit.
- Critical architecture checkpoints require project-reference direction, no-new-partial proof, direct tests of extracted services/components, source assertions that pages/services did not absorb the new reusable logic, and a downstream unlock decision.

## Evidence Contract

- Targeted `dotnet test` commands for component, unit, integration, and Playwright projects, recorded per subbundle.
- `dotnet build CanDoItAll.slnx --no-restore` after each architecture-affecting phase and before final closure.
- PostgreSQL migration generation/application verification when contact-point tags add a column.
- Prepared and completed bundle-validator results.
- Normal and applicable open-overlay screenshots under `evidence/browser/SBxx/` at `1800x1100`, with first-viewport, scroll-owner, clipping, focus, layering, loading, empty, error, and action-visibility findings.
- Proof tier: `Behavioral` for SB01, SB02, SB03, SB04, SB05, and SB06.

## UI Validation Strategy

- Target `1800x1100`; `1600x900` is the minimum fallback if the environment cannot expose the preferred viewport. No narrower application pass is planned.
- Primary surface: searchable record/opportunity list or selected CRM account workbench; filters and compact counts support rather than compete with it.
- Stats treatment: compact status badges/strips for supporting counts; Financials alone promotes metrics/charts to the primary surface.
- List/editor organization: list remains visible; independent create/edit flows use `Dialog`; selected account subviews use tabs; no stacked permanent opportunity editor.
- Text areas use semantic BaseLib sizing or explicit rows appropriate to content; contact wizard uses a medium dialog, record/project picker a wide dialog, opportunity create/edit a wide dialog, and detail a medium/wide read-only dialog.
- First viewport: compact header/navigation, filters, and useful list/detail content visible without page scrolling.
- Scroll owner: the existing routed workbench/list-detail panel owns page scrolling; dialog body owns overlay scrolling; the shared record browser must not introduce a second nested vertical scroll owner.
- Components MCP transport was intermittent. A successful parallel recommendation selected BaseLib `Dialog`, `FormStack`/`FormSection`/`FormRow`/`FormField`, `DataGrid`, `ListDetailShell`, and typed `CdaChart`; a subsequent library-list call failed with `Transport closed`. Existing source usage and `compact-ui-composition.md` remain the setup fallback.

## Browser Validation Analytics

- Each SB row in `reviews/01-execution-report.md` records route, viewport, Playwright actions/assertions, normal/open-overlay screenshot paths, first-viewport finding, actual scroll owner, constrained-container behavior, and result.
- Required routes are `/crm-hr/assignments`, `/crm-hr/directory`, `/crm-hr/crm`, and the workbench shell with multiple CRM/HR tabs.
- Required overlays include party/record picker, contact wizard, relationship picker, opportunity create/detail/edit, project picker, and any filter dropdown/tooltip whose layering could clip.

## Working Assumptions

- Contact-method tags are contact-specific and therefore require persisted `PartyContactPoint.TagsJson`, not an implicit mutation of party-level tags.
- Won opportunity amounts are the only current defensible sold signal. There is no purchase or invoice model, so bought and overdue metrics remain explicitly unavailable.
- `CanDoItAll.AppComponents` is the correct neutral owner for a reusable typed record browser; CRM/HR and Projects provide adapters/query services.
- A typed static CRM route catalog shared by `CrmHrSecondaryTabs` and Web workbench descriptor resolution is the smallest safe contextual-title seam. `IShellNavigationContributor` is not metadata-only: current composition flattens its items into main navigation.

## Primary Risks

- Loading all parties/projects and paginating only in memory would falsely satisfy the UI while failing the thousand-record requirement.
- Reusing `CrmHrAgentQueryService` would leak agent-specific search/redaction semantics into UI selection and still lacks offset/page totals.
- Extending the large partial services/pages would deepen the monolith and make isolated tests impossible.
- Contact tags require migration and import/export/merge/source-snapshot compatibility review.
- Opportunity and Financials both edit the CRM page; parallel execution risks merge conflicts and invalid proof.
- Shell title changes can accidentally collapse tab identity or expose unwanted navigation entries.
- CodeAnalytics and intermittent Components transport reduce automated evidence; final claims must distinguish the successful component recommendation from the missing library/setup and architecture snapshot results.
