# SB04 Opportunity Workspace And Project Selection

## Status

- `Completed`

## Objective

- Replace the stacked opportunity board/editor with a compact, reusable, server-paged pipeline plus isolated create, detail, and edit dialogs that use scalable party and project selection.

## Success Criteria

- Opportunity search/filter/page operations execute before materialization with deterministic ordering.
- Filters occupy at most two rows at `1800x1100`; owner selection uses the shared party picker.
- Add opens a wizard, selecting a result opens read-only detail, and Edit opens an isolated wide dialog.
- Related projects are searched and paged through a Projects-owned query; explicit clear and missing-project validation work.
- Opportunity values are never summed across currencies.

## Covered Inputs

- `N006`, `N007`, `N008`; `R008`, `R009`, `R010`, `R013`, `R014`.

## Prerequisites

- SB03 and `CP-03` passed.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/OpportunityBoard.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/OpportunityEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/OpportunityConversionDialog.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Models/CrmHrBusinessModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `repo://tests/Components/CanDoItAll.Tests.Components/OpportunityBoardTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/OpportunityPipelineTests.cs`
- `bundle://design/ui-proposals/04-create-opportunity-wizard.png`
- `bundle://design/ui-proposals/05-opportunity-detail-dialog.png`
- `bundle://design/ui-proposals/06-project-picker-dialog.png`

## UI Composition Contract

- Primary surface: paged opportunity list/pipeline with Add action; filters and totals are supporting content.
- Stats treatment: compact stage/count/value badges, always currency-labelled and never cross-currency summed.
- List/editor organization: no permanent editor; create wizard, read-only detail, and wide edit dialog use isolated drafts.
- Textarea/dialog sizing: summary/notes use practical minimum heights; create is wide step-based, detail/edit are wide with stable footer actions.
- First viewport: header, compact filters, and useful result rows/cards fit at `1800x1100`; no introductory card mosaic.
- Scroll owner: page/list pane for results and dialog body for overlays; overlay footer remains visible.

## Deliverables

- Cohesive opportunity query contract/service and reusable pipeline component.
- Typed filter state with compact composition and scalable owner selection.
- Create wizard, detail dialog, and edit dialog.
- Projects-owned paged selection service and reusable project picker integration.
- Updated conversion flow and Behavioral tests.

## Dependency Impact

- SB05 adds Financials to the same CRM page and depends on the opportunity query/model truth established here.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-04`.

## C# Architecture Impact

- Moves reusable filtering/paging/dialog behavior out of the 1,810-line page and prevents further growth of the 6,054-line service.

## Boundary Ownership

- CRM/HR owns opportunity persistence/query/dialog orchestration; Projects owns project search; AppComponents owns neutral browser mechanics.

## Dependency Direction

- CRM/HR may consume Projects contracts; Projects must not reference CRM/HR; shared UI remains domain-neutral.

## Pattern Decision

- Typed Query Object/Strategy for paging, Adapter for shared browser records, and explicit wizard state for creation.

## Testability Contract

- Query, filters, wizard, detail, and edit behaviors instantiate without `CrmHrCrmPage`; cancel tests prove drafts do not mutate persisted state.

## Partial Class Policy

- No new page/service partial or nested query service.

## Architecture Proof Required

- Direct query/dialog tests, SQL-bounded paging proof, Projects ownership assertion, old-page shrink/thin orchestration evidence, no-new-partial audit, build, and browser composition.

## Implementation Steps

1. Characterize current save, stage, conversion, and linked-project behavior.
2. Add CRM opportunity and Projects selection query services with stable server paging.
3. Extract the typed compact pipeline and remove render-time page filtering/materialization.
4. Add create wizard, detail, and edit dialogs with isolated models and shared party/project pickers.
5. Update project conversion selection and explicit clear/missing-id behavior.
6. Rewrite targeted component/integration/Playwright proof and run `CP-04`.

## Scope Exceptions

- No forecasting, currency conversion, or project-management redesign.

## Do Not Do

- Do not keep the permanent editor, load all projects/parties/opportunities for a picker, infer currency, or hide failed queries behind stale/in-memory results.

## Acceptance Checklist

- [x] Paging/filtering is source-side and stable.
- [x] Filters use at most two large-screen rows.
- [x] Create/detail/edit are independent dialog flows.
- [x] Cancel preserves current list/filter/persisted state.
- [x] Related project selection is scalable and clearable.
- [x] Currency presentation is honest.

## Execution Evidence

- Shipped behavior: `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/OpportunityPipeline.razor` is the compact primary surface; create, detail, and edit use separate controlled dialogs; party and project choices use typed paged pickers.
- Boundary proof: `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectRecordQueryService.cs` owns project search and CRM/HR consumes its contract in the allowed direction.
- Semantic positive proof: `repo://tests/Integration/CanDoItAll.Tests.Integration/RecordQueryIntegrationTests.cs` proves opportunity filtering/paging before materialization and Projects-owned search paging; conversion tests prove Won conversion creates the project and preserves assignments.
- Adversarial negative proof: `repo://tests/Integration/CanDoItAll.Tests.Integration/OpportunityIntegrityIntegrationTests.cs` rejects invalid identity/currency/party policy, stale edits, and invalid project references; component tests prove create cancel does not mutate the caller and mixed currencies are never rendered as one total.
- Browser proof: pipeline, create, detail, edit, and project-picker screenshots under `repo://output/playwright/crm-hr-feedback10/`, including `final-crm-opportunity-pipeline-1800x1100.png`.
- Progression decision: `CP-04 passed`; SB05 consumed the validated opportunity/recognition truth.

## Proof Required

- Raw notes owned: `N006`, `N007`, `N008`.
- Shallow-pass trap: visually hiding the inline editor while retaining eager full-list filters/dropdowns.
- Adversarial negative proof: later-page result, duplicate titles, stale query completion, cancel after edits, missing project, and mixed currencies.
- Semantic positive proof: create, open detail, edit owner/project, save/reload, clear project, and convert through production services.
- Anti-stub audit: no fallback list, placeholder dialog, TODO, fake project result, or page-local duplicate query engine.

## Browser Validation Logging

- Route: `/crm-hr/crm?accountId=<seeded-account>`.
- Viewport: `1800x1100`.
- Actions: filter/page/reset, owner picker, Add wizard, save, open detail, edit/cancel/save, project picker select/clear, conversion.
- Screenshots: `bundle://evidence/browser/SB04/opportunity-pipeline.png`, `bundle://evidence/browser/SB04/opportunity-create.png`, `bundle://evidence/browser/SB04/opportunity-detail.png`, `bundle://evidence/browser/SB04/project-picker.png`.
- Review: one dominant surface, filter density, no clipping/overflow, clear focus, stable footer, useful first viewport, one scroll owner.

## Progression Gate

- SB05 starts only after `CP-04`, bounded query proof, create/detail/edit persistence proof, and project-picker browser proof pass.

## Reopen Triggers

- Reopen for eager lists, stale overwrite, draft leakage, missing currency labels, project-boundary inversion, picker fallback, or dialog/pipeline browser failure.

## Suggested Agent Prompt

```text
Implement SB04 only. Extract the bounded opportunity pipeline and Projects-owned selector, replace the permanent editor with isolated create/detail/edit dialogs, prove currency-safe behavior and production persistence, update CP-04/report, and stop on any eager-list fallback or boundary inversion.
```
