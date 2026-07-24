# SB02 Scalable Record Pickers And Tag Consistency

## Status

- `Completed`

## Objective

- Apply the SB01 browser/query contract across CRM/HR entity selectors and ordinary searchable lists, and replace every mutable CRM/HR tag surface with BaseLib `TagEditor`.

## Success Criteria

- Directory/account/workforce/recruiting/agent/assignment primary lists use server-paged query semantics or record an explicit bounded exception; opportunity list is deliberately deferred to SB04.
- All audited party/owner/delivery/relationship selectors use the typed picker or have a documented finite bound.
- Directory party tags and picker tag filters use `TagEditor`; comma-delimited `TagsText` editing is gone.
- A 1,001-record SQL-capture test proves bounded query execution, stable pages, search, conjunctive tags, and people/organization scopes.

## Covered Inputs

- `N002`, `N004`, `N005`.
- `R002`, `R006`, `R007`, `R013`, `R014`.

## Prerequisites

- SB01 and `CP-01` passed; no fallback path remains.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrWorkforcePage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrRecruitingPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyRelationshipsEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/NextActionEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/StaffingRequestEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/CandidatePipeline.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/WorkflowCatalogSearchPersistenceIntegrationTests.cs`

## UI Composition Contract

- Primary surface: searchable paged list; compact filters/counts support it.
- Stats treatment: counts/badges, never metric cards.
- List/editor organization: existing `ListDetailShell`; independent selection uses wide picker dialog.
- Textarea/dialog sizing: no new textarea; picker wide.
- First viewport: header, filters, meaningful page of records, and selected detail visible at `1800x1100`.
- Scroll owner: existing list/detail pane or dialog body; no nested results scroll.

## Deliverables

- Typed DB-backed list/picker query adapters and selector audit closure.
- TagEditor migration for party tags and tag filters.
- Standard-list reuse through the same browser core.
- Query/component/browser tests.

## Dependency Impact

- SB03 consumes relationship picker/tag conventions; SB04 consumes owner/party/list semantics.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-02`.

## C# Architecture Impact

- Replaces page-local list/filter logic with direct-testable query adapters without extending large partial services.

## Boundary Ownership

- Pages orchestrate; module query services own EF filtering/paging; AppComponents owns browser mechanics.

## Dependency Direction

- No new project edge beyond SB01; no CRM types move into AppComponents.

## Pattern Decision

- Reuse SB01 Strategy/Adapter; no second list protocol.

## Testability Contract

- Direct query tests plus component tests; SQL interceptor asserts bounded result command (`LIMIT`/equivalent).

## Partial Class Policy

- No new partial/page-partial. New queries are top-level cohesive types.

## Architecture Proof Required

- Selector/list audit, no comma tag editor, SQL bounds, old page-filter shrink, no-new-partial, solution build, downstream relationship/list smoke.

## Implementation Steps

1. Inventory every entity-valued `InputSelect`; keep enum/status controls.
2. Add typed query adapters for standard CRM/HR lists and party scopes.
3. Cut primary lists and high-cardinality selectors to the shared browser; defer only opportunity list to SB04.
4. Replace Directory `TagsText` and tag filters with `TagEditor`; normalize once at boundary.
5. Add SQL-capture, stable-page, tag/type/search, and rendered state tests.
6. Capture Directory plus representative cross-route browser proof and run `CP-02`.

## Scope Exceptions

- Opportunity list/filters and project picker are SB04. Any other retained entity dropdown/list needs an explicit bounded-cardinality entry in the execution report.

## Do Not Do

- Do not label in-memory `Skip/Take` as scalable, replace finite enums, add comma parsing, or duplicate browser logic.

## Acceptance Checklist

- [x] All mutable CRM/HR tags use TagEditor.
- [x] Directory and standard lists share server query/paging semantics.
- [x] People/organizations scopes and conjunctive tags work.
- [x] Selector audit has no unexplained high-cardinality dropdown.
- [x] SQL-capture and >1,000 stable-page tests pass.

## Execution Evidence

- Shipped behavior: Directory, account/workspace, workforce, recruiting, agent, project-assignment, stakeholder, owner, manager, and home-unit flows use typed paged query/picker contracts; mutable party tags and picker tag filters use the shared tag component.
- Source proof: `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyRecordQueryService.cs` applies privacy filtering before search/tag predicates and pages before materialization; affected pages orchestrate the query rather than receiving full catalogs.
- Semantic positive proof: `Party_query_applies_stable_source_paging_scope_and_conjunctive_tags` in `repo://tests/Integration/CanDoItAll.Tests.Integration/RecordQueryIntegrationTests.cs` proves the people/organization, search, tag, total, and stable-page contract.
- Adversarial negative proof: the same integration file proves sensitive tags/names are redacted before indexing/filtering and that a partial conjunctive-tag match is excluded. Component tests prove no options/full-list party-picker fallback is accepted.
- Browser proof: `repo://output/playwright/crm-hr-feedback10/final-directory-1800x1100.png` plus the representative Assignments and CRM screenshots in `repo://output/playwright/crm-hr-feedback10/`.
- Progression decision: `CP-02 passed`; the relationship and opportunity work consumed the same selector/query foundation.

## Proof Required

- Raw note owned: literal `N002`, `N004`, `N005`.
- Shipped behavior/source/test proof with exact audit table and commands.
- Shallow-pass trap: cosmetic picker/list over an eagerly loaded collection.
- Adversarial negative proof: desired item only beyond page one; two-tag partial match excluded; duplicate display names stable.
- Semantic positive proof: Directory relationship and standard list find/select filtered person and organization.
- Anti-stub audit: no fallback, placeholder provider, TODO, or duplicate local filter engine.

## Browser Validation Logging

- Routes: `/crm-hr/directory`, `/crm-hr/crm`, and one representative workforce/recruiting/agents route.
- Viewport: `1800x1100`.
- Actions: edit tags, filter by tags/type, page/search list, open/select relationship picker, test loading/empty/error.
- Screenshots: `bundle://evidence/browser/SB02/directory-list-tags.png`, `bundle://evidence/browser/SB02/relationship-picker-open.png`, `bundle://evidence/browser/SB02/standard-list.png`.
- Review: dominant list, compact controls, one scroll owner, constrained dialog layout, no clipping.

## Progression Gate

- SB03/SB04 may proceed only after `CP-02` passes and selector/tag audits have no unexplained exception.

## Reopen Triggers

- Reopen for any mutable non-TagEditor tag field, full-list query, inconsistent list engine, unexplained entity dropdown, unstable page, or stale result.

## Suggested Agent Prompt

```text
Implement SB02 only. Apply the SB01 browser/query contract across CRM/HR lists and entity selectors, replace every mutable tag surface with TagEditor, prove SQL-bounded >1,000-record behavior and large-screen composition, update CP-02/report, and stop on any unexplained fallback.
```
