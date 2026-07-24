# SB06 Contextual Tabs And Final Hardening

## Status

- `Completed`

## Objective

- Give each CRM/HR workbench route a concise contextual tab title, then run the cross-cutting architecture, performance, regression, and large-screen closure gates.

## Success Criteria

- Directory, CRM, Workforce, Recruiting, Agents, and Assignments tabs have distinct concise titles while preserving route/id/restore identity.
- Main navigation remains one CRM/HR entry and non-CRM title behavior is unchanged.
- No new feature partials, eager high-cardinality fallbacks, callback-index hazards, UI financial calculations, or mixed-currency sums remain in affected paths.
- Targeted suites, full solution build/tests, migration checks, and inspected large-screen browser flows pass.

## Covered Inputs

- `N010`; `R012`, `R013`, `R014`, `R015`; final closure for `N001`-`N010`.

## Prerequisites

- SB01-SB05 and `CP-01` through `CP-05` passed with evidence.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/CrmHrSecondaryTabs.razor`
- `repo://src/App/CanDoItAll.Web/Components/Layout/MainLayout.Workbench.cs`
- `repo://src/App/CanDoItAll.Web/Composition/ShellNavigation.cs`
- `repo://src/UI/CanDoItAll.AppComponents/Components/AppTabStrip.razor`
- `repo://tests/Components/CanDoItAll.Tests.Components/CrmHrNavigationTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/MainLayoutDatabaseProfileTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/CrmHrShellSmokeTests.cs`
- `bundle://reviews/csharp-architecture-gate.md`
- `bundle://reviews/01-execution-report.md`

## UI Composition Contract

- Primary surface: unchanged workbench content; only tab-strip labels become contextual.
- Stats/list/editor/textarea treatment: unchanged from owning routes.
- First viewport: distinct label token appears before `9rem` truncation.
- Scroll owner: unchanged page/detail/dialog ownership.
- Final visual pass: all affected normal and open-overlay surfaces at `1800x1100`.

## Deliverables

- Typed CRM route/title catalog reused by secondary navigation and workbench descriptor composition.
- Contextual titles with stable ids/restore keys and unchanged main navigation.
- Final architecture/performance/source audits, regression tests, browser evidence, raw-note closure, and completed bundle validation.

## Dependency Impact

- Final closure only; any discovered regression reopens the owning earlier subbundle.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-06` plus final review gate.

## C# Architecture Impact

- Centralizes route metadata without abusing shell navigation contributors or introducing stringly typed duplicated maps.

## Boundary Ownership

- CRM/HR owns route metadata; Web consumes it when constructing display descriptors; workbench keeps identity ownership.

## Dependency Direction

- Use the existing Web-to-module reference direction; do not add CRM/HR-to-Web or duplicate main-navigation registrations.

## Pattern Decision

- Immutable typed route catalog with exact route matching before generic shell fallback.

## Testability Contract

- Catalog/descriptor tests instantiate without browser; layout and Playwright tests prove simultaneous visible tabs and restore identity.

## Partial Class Policy

- Existing `MainLayout.Workbench.cs` remains acceptable UI source organization; no new feature partial is added.

## Architecture Proof Required

- Route theory, navigation/layout tests, stable-id assertions, project/reference build, no-new-partial/no-fallback/old-owner audits, performance scan, all downstream browser surfaces, and final architecture gate.

## Implementation Steps

1. Add typed CRM route/title catalog and reuse it in secondary tabs.
2. Consult the catalog in workbench descriptor construction before generic navigation fallback.
3. Add route/title, simultaneous-tab, stable-id, main-navigation, and non-CRM regression tests.
4. Run targeted and broad build/test/migration validation.
5. Run the two-pass .NET performance audit and repair in-scope hot-path findings.
6. Inspect large-screen normal/open-overlay screenshots, close raw notes, run completed-stage validator, and finalize `CP-06`.

## Scope Exceptions

- Dynamic customer/person names are not required in titles; static route context avoids sensitive-data exposure.

## Do Not Do

- Do not add child routes to main navigation, change tab identity to display text, duplicate magic route strings, widen responsive scope, or close on source-only/browser-only proof.

## Acceptance Checklist

- [x] Every CRM/HR route has a distinct concise title.
- [x] Tab ids/routes/restore keys remain stable.
- [x] Main navigation and non-CRM tabs are unchanged.
- [x] Architecture/performance audits have no unreviewed in-scope blocker.
- [x] Tests/build/browser/completed validator pass.
- [x] Every raw note has proof or explicit N/A.

## Execution Evidence

- Shipped behavior: the typed catalog under `repo://src/Modules/CanDoItAll.Modules.CrmHr/Navigation/` supplies distinct Directory, CRM, Workforce, Recruiting, Agents, and Assignments labels; Web uses it for display while retaining route/id identity and one root CRM/HR navigation item.
- Semantic positive proof: `repo://tests/Components/CanDoItAll.Tests.Components/CrmHrNavigationTests.cs` covers all child routes, distinct keys/titles, contextual workbench descriptors, stable ids, and the single shell-navigation entry.
- Adversarial negative proof: source/unit/integration audits cover public-contact masking, platform-stable agent context payloads, persisted AI-agent projections without page-time catalog enumeration, bounded source-snapshot related queries, and lazy assignment/directory history.
- Home closure proof: stale bundle-era Home/CRM copy was removed; `CrmHrHomeQueryService.AgentProjectionCount` counts only bound `AiResourceBinding` AgentFramework projections instead of legacy `AiAgentProfile` rows; and the UI label is `Agent projections`. Focused integration test `Home_agent_count_uses_only_bound_agent_framework_projections` in `repo://tests/Integration/CanDoItAll.Tests.Integration/StaffingAllocationIntegrationTests.cs` rebuilt and passed `1/1`.
- Performance proof: source snapshot queries page before related fetches; CSV duplicate lookup is capped and batched; activity/audit history has typed paging; the high-cardinality migration adds normalized party, interaction, audit, and persisted-agent projection indexes/fields.
- Validation proof: the final Release solution build completed with zero errors, focused affected tests passed in their primary run or exact repaired-case rerun, application startup returned HTTP 200 with migrations applied, EF drift was empty, and `git diff --check` returned zero.
- Honest baseline: the earlier broad all-suite run remained non-green for unrelated repository failures and is not claimed as passing; `System.Security.Cryptography.Xml` `10.0.7` `NU1903` advisories remain a security follow-up.
- Browser proof: the normal and overlay images listed in `bundle://reviews/01-execution-report.md` were inspected at `1800x1100`, including the overwritten `repo://output/playwright/crm-hr-feedback10/final-home-1800x1100.png` showing corrected copy and `34` `Agent projections`; browser console error count was zero and final4 logs contained no `fail`, `Unhandled`, or `Exception`.
- Progression decision: `CP-06` and the final architecture/performance gate passed; completed-stage bundle validation passed.

## Proof Required

- Raw note owned: `N010`; closure audit owns `N001`-`N010`.
- Shallow-pass trap: changing browser PageTitle or group name without changing workbench tab descriptors.
- Adversarial negative proof: Directory and CRM open together, truncated labels, restore session, root CRM nav, and non-CRM tab.
- Semantic positive proof: six child routes show distinct labels/routes concurrently.
- Anti-stub audit: no duplicate nav entries, display-title identity key, magic-string map copies, TODO, or skipped earlier gate.

## Browser Validation Logging

- Routes: all six CRM/HR child routes plus representative contact/picker/opportunity/Financials overlays.
- Viewport: `1800x1100`.
- Actions: open routes as workbench tabs, assert labels/hrefs/ids; restore/revisit; replay critical flows; inspect console/error UI.
- Screenshots: `bundle://evidence/browser/SB06/contextual-tabs.png`, `bundle://evidence/browser/SB06/final-directory-contact.png`, `bundle://evidence/browser/SB06/final-opportunity.png`, `bundle://evidence/browser/SB06/final-financials.png`.
- Review: distinct early tokens, no truncation ambiguity, no overlap/clipping, consistent density, correct focus/footer/scroll ownership, no Blazor error banner.

## Progression Gate

- Final closure requires `CP-06`, completed-stage validator, raw-note table, execution report, architecture review, performance checklist, and all applicable browser evidence to pass.

## Reopen Triggers

- Reopen the owning subbundle for duplicate titles/ids, nav pollution, test/build failure, migration drift, performance regression, browser error, weak evidence, or any unclosed raw note.

## Suggested Agent Prompt

```text
Implement SB06 only. Centralize CRM route titles without changing identity/navigation, run the complete architecture/performance/regression/browser closure, reopen any weak earlier work, update CP-06/report/raw-note closure, and do not mark complete until the completed-stage validator passes.
```
