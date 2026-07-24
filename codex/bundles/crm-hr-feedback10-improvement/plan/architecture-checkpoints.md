# Architecture Checkpoints

Every checkpoint is a blocking decision. Record results in `reviews/01-execution-report.md` and obtain the review defined in `reviews/csharp-architecture-gate.md`.

## CP-01 After SB01: Shared Picker Foundation

- Dependency graph review: inspect before/after project references; prove `CrmHr -> AppComponents` and no reverse/cyclic edge.
- Boundary review: AppComponents contains only neutral typed contracts/mechanics; CRM/HR/Projects types remain in their modules.
- Partial-class review: no new partial or nested service.
- Testability review: shared browser and CRM adapter instantiate directly with fake loader/seeded persistence.
- Old-owner proof: `PartyPicker` is removed or thin; no full-list/dropdown fallback; reusable mechanics are absent from large pages/services.
- Negative proof: >1,000-record/stale-request/full-list shallow implementation is rejected.
- Downstream check: a real existing assignment/allocation party selection flow works through the new boundary.
- Unlock: only an explicit `Pass` unlocks SB02. Failure reopens SB01 and blocks SB02-SB06.

## CP-02 After SB02: Cross-Form And Tag Consistency

- Selector audit: every CRM/HR entity selector is migrated or has a bounded-cardinality exception.
- Query review: search/tags/type/page execute in the domain query, not after full materialization.
- Tag review: every mutable CRM/HR tag surface uses `TagEditor`; no `TagsText` comma parser remains.
- Page shrink/thin proof: Directory/CRM/other pages orchestrate browser loaders rather than owning generic filter/paging logic.
- Testability review: query and mapping tests run without constructing large services/pages.
- Downstream check: Directory relationship selection and one ordinary list use the same browser core.
- Unlock: Pass unlocks SB03 and the SB04 dependency branch. Failure reopens SB01 or SB02 according to ownership.

## CP-03 After SB03: Dialog Draft And Persistence Safety

- Boundary review: contact wizard state is isolated in a component/state type; Directory page only opens/closes and handles result.
- Persistence review: contact tags cover entity configuration, mapping, migration/default, import/export/merge, and relevant snapshot/redaction behavior.
- Partial-class review: no feature partial was added to Directory or services.
- Negative proof: add-empty-cancel/remove and reorder/remove cannot throw or mutate the wrong record.
- Downstream check: relationship picker still uses the SB01/SB02 boundary.
- Unlock: Pass unlocks SB04. Failure reopens SB03 and blocks SB04-SB06.

## CP-04 After SB04: Opportunity Workspace

- Boundary review: filters/pipeline/create/detail/edit/project picking are cohesive components/services; CRM page is orchestration.
- Dependency review: Projects owns project query mapping; no `Projects -> CrmHr` reference.
- Old-owner proof: permanent stacked `OpportunityEditor` is absent; page-local filter predicates and full-list owner/project dropdowns are removed.
- Testability review: dialogs/pipeline/project adapter instantiate directly.
- Negative proof: cancel cannot persist draft owner/project changes; page-two project/party can be selected; route/dialog state remains consistent.
- Downstream check: opportunity selection/save/reload/conversion still works.
- Unlock: Pass unlocks SB05. Failure reopens SB04 and invalidates opportunity proof used by SB05.

## CP-05 After SB05: Financial Projection

- Boundary review: aggregation and availability live in a direct-testable query/projection, not Razor or `CrmService`.
- Dependency review: projection returns domain/presentation records and does not reference chart types.
- Data-honesty review: currency separation; bought and overdue unavailable; no seeded/fake production series.
- Testability review: positive/negative projection tests plus actual `CdaChart` rendering.
- Old-owner proof: CRM page only requests/renders snapshot state.
- Unlock: Pass unlocks SB06. Failure reopens SB05 and blocks final closure.

## CP-06 After SB06: Workbench Metadata And Final Architecture Gate

- Route-catalog review: CRM metadata is centralized and consumed without adding flattened child entries to main navigation or scattering route strings.
- Identity review: contextual titles and stable restore/tab ids coexist for multiple CRM/HR routes.
- Full dependency/partial audit: rerun all source/project checks and solution build.
- CodeAnalytics status: capture scoped snapshot/cycle result if transport is restored; otherwise preserve explicit unavailable gap and manual proof.
- Regression review: no prior foundation reopened; all raw notes have Behavioral evidence.
- Closure: `csharp-architecture-review-gate` and completed bundle validator must pass.
