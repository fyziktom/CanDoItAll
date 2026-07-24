# SB01 Architecture And UI Design Foundation

## Status

- `Completed`

## Objective

- Establish the domain-neutral, strongly typed, async paged record-browser foundation and prove one real CRM/HR party-selection flow uses it without a full-list/dropdown fallback.

## Success Criteria

- AppComponents owns typed request/page/option records, cancellation-aware loader strategy, browser body, and wide dialog host.
- Search, conjunctive tags, typed filter, stable paging, total count, loading, empty, error, retry, and typed selection are directly testable.
- CRM/HR references AppComponents in the allowed direction; `PartyPicker` is removed or a thin loader adapter.
- A real assignment/allocation picker renders and selects through the new foundation at large desktop.

## Covered Inputs

- `N004` shared scalable picker foundation.
- `N005` reusable browser-body foundation.
- `R005`, `R006`, `R013`, `R014`.

## Prerequisites

- Prepared-stage bundle validation passes.
- Architecture preparation gate in `bundle://reviews/csharp-architecture-gate.md` is accepted.

## Exact Source References

- `repo://src/UI/CanDoItAll.AppComponents/Components/ResourceCardPicker.razor`
- `repo://src/UI/CanDoItAll.AppComponents/Components/ResourceCardPickerOption.cs`
- `repo://src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- `repo://src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGallerySearchList.razor`
- `repo://src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGalleryPickerDialog.razor`
- `repo://src/Modules/CanDoItAll.Modules.Prompts/PromptGalleryContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Prompts/EfPromptGallerySearchDriver.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyPicker.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `repo://tests/Components/CanDoItAll.Tests.Components/ResourceCardPickerTests.cs`

## UI Composition Contract

- Primary surface: paged record results; search, `TagEditor`, typed scope, result count, pager, and retry are supporting controls.
- Stats treatment: compact result/page badges only.
- List/editor organization: reusable browser body inside a wide BaseLib `Dialog`; standalone consumers embed the same body.
- Textarea/dialog sizing: no textarea; wide dialog because cards/grid and filters need horizontal scan space.
- First viewport: filters, first result page, and pager/action footer are useful at `1800x1100`.
- Scroll owner: dialog body or existing list pane, never a nested browser viewport.
- Visual thesis: calm, dense, professional; card/list selection feedback is restrained and functional.

## Deliverables

- Neutral AppComponents browser/picker contracts and components.
- CRM/HR typed loader adapter and directionally safe project reference.
- Thin/removed `PartyPicker` with updated assignment/allocation consumer.
- Direct tests and one downstream browser composition check.

## Dependency Impact

- Critical foundation for SB02-SB06; weak paging, typing, cancellation, or dependency proof invalidates every downstream selector/list result.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: yes; checkpoint `CP-01`.

## C# Architecture Impact

- Adds a shared UI/application boundary and module adapters; must reduce rather than relocate responsibility concentration.

## Boundary Ownership

- AppComponents owns neutral mechanics; CRM/HR owns EF/domain filters and mapping.

## Dependency Direction

- Allow `CrmHr -> AppComponents`; forbid AppComponents references to CRM/HR or Projects; prove no cycle.

## Pattern Decision

- Use the typed async loader Strategy and domain Adapter records in `architecture/03-csharp-pattern-selection-records.md`.

## Testability Contract

- Fake loaders test component behavior; seeded query tests instantiate adapter without large services/pages.

## Partial Class Policy

- No new partial/nested service. Do not add picker behavior to `CrmHrServices.cs`.

## Architecture Proof Required

- Before/after references, build, no-domain-import/no-new-partial audit, direct tests, `PartyPicker` thin/removal source assertion, >1,000-record negative proof, and real consumer smoke.

## Implementation Steps

1. Characterize existing ResourceCardPicker and Prompt Gallery behavior.
2. Add the smallest neutral typed request/page/option/loader contract and reusable browser/dialog.
3. Implement debounce/cancellation/stale-response/error/pager behavior.
4. Add CRM/HR query adapter and DI; add the allowed project reference.
5. Cut `PartyPicker` and one real assignment/allocation consumer to the new loader with no options fallback.
6. Run direct tests, reference/build checks, and large-screen consumer proof.

## Scope Exceptions

- Cross-form adoption and tag consistency are SB02; opportunities/projects are SB04.

## Do Not Do

- Do not copy Prompt domain types, use string ids/kinds, page after full materialization, add a service locator, or keep a silent dropdown fallback.

## Acceptance Checklist

- [x] Loader query is typed/cancelable and returns total-bearing pages.
- [x] Stable SQL/data paging survives >1,000 records.
- [x] Loading/empty/error/retry and stale-request behavior are explicit.
- [x] Selection returns typed key.
- [x] AppComponents remains domain-neutral and project graph is acyclic.
- [x] Real CRM/HR consumer works through the new boundary.

## Execution Evidence

- Shipped behavior: `repo://src/UI/CanDoItAll.AppComponents/Components/PagedRecordBrowser.razor`, its typed contracts, and the generic picker dialog own neutral paging mechanics; CRM/HR adapts them through `PartyRecordQueryService` and the thin party picker components.
- Semantic positive proof: `repo://tests/Integration/CanDoItAll.Tests.Integration/RecordQueryIntegrationTests.cs` proves stable party paging across 1,001 records, scoped search, and conjunctive tags; the Assignments workspace rendered through the new boundary.
- Adversarial negative proof: `repo://tests/Components/CanDoItAll.Tests.Components/PagedRecordBrowserTests.cs` covers stale response suppression, explicit loader failure/retry, loading, empty pages, and page reset after filter changes.
- Architecture proof: source/reference review found no CRM/HR import in AppComponents and the Release solution build passed with zero errors.
- Browser proof: `repo://output/playwright/crm-hr-feedback10/final-assignments-1800x1100.png`.
- Progression decision: `CP-01 passed`; downstream selector work was allowed to proceed.

## Proof Required

- Raw note owned: `N004`/`N005` foundation.
- Shipped behavior and source proof: exact production files and old-owner assertion.
- Test proof: targeted AppComponents/component/integration commands plus solution build.
- Shallow-pass trap: client-side paging of a supplied list or hidden dropdown fallback.
- Adversarial negative proof: 1,001 records, page-bound result, stale older response, and loader failure.
- Semantic positive proof: search/tag/type/page selects a record from a later page in a real consumer.
- Anti-stub audit: no TODO, fixture branch, `NotImplementedException`, fake loader in production, or domain imports.

## Browser Validation Logging

- Route: `/crm-hr/assignments`.
- Viewport: `1800x1100`.
- Actions: open party picker, search/filter/page/select/cancel/reopen; assert focus and selected summary.
- Screenshots: `bundle://evidence/browser/SB01/party-picker-normal.png`, `bundle://evidence/browser/SB01/party-picker-open.png`.
- Review: first page useful, one scroll owner, no clipping/lateral overflow, footer actions visible, restrained loading/selection feedback.

## Progression Gate

- SB02 starts only after `CP-01` passes and the downstream party-selection smoke proves the new boundary is active.

## Reopen Triggers

- Reopen for any untyped contract, eager full-list path, stale overwrite, hidden fallback, domain import in AppComponents, cycle, new partial, or consumer bypass.

## Suggested Agent Prompt

```text
Implement SB01 only. Build the neutral typed async paged browser and CRM adapter, cut one real party-selection flow to it, prove scale/cancellation/dependency direction and large-screen dialog behavior, update the execution report and CP-01, and stop on any fallback or cycle.
```
