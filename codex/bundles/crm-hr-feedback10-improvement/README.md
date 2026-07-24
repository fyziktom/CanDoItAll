# CRM-HR Feedback 10 Improvement

This initiative-profile bundle converts `feedback10.docx` into six dependency-ordered, implementation-ready work units for the CRM/HR module and its shared application UI seams.

## Profile

- `initiative` upgraded from concrete feedback because the request introduces a shared paged picker, cross-module project selection, persistence changes, chart projections, and shell-composition behavior.

## Mission

- Make CRM/HR usable with hundreds or thousands of records: consistent tag editing, safe dialog-first contact entry, scalable typed record selection, a compact opportunity workspace, honest financial insight, and contextual workspace-tab titles.
- Reduce responsibility pressure on the 6,054-line `CrmHrServices.cs`, 1,810-line CRM page, and 1,866-line Directory page by placing new reusable behavior in cohesive top-level components and query services.

## Outcome Contract

- Requested outcome: close raw notes `N002` through `N010`; classify `N001` as the source section heading and therefore informational.
- Hard constraints: use existing BaseLib wrappers and the repository's Tailwind theme; use no Radzen components; keep application-page design and proof large-desktop-only; keep selection/query contracts strongly typed; show loading, empty, and failure states explicitly; do not add silent fallback behavior.
- Evidence required before closure: targeted component/unit/integration tests, PostgreSQL migration proof if contact-point tags require storage, solution build, realistic positive and adversarial negative Behavioral proof for every subbundle, and inspected Playwright screenshots at `1800x1100` for normal and relevant open-overlay states.
- Known evidence limitations or explicit scope exceptions: CodeAnalytics and Components transports were unavailable during final execution, so automated architecture/component-library snapshots are absent. Direct source inspection, dependency review, focused tests, the Release solution build, and rendered browser inspection provide the closure evidence instead. Small/medium/mobile application work, invoice management, purchasing records, and fabricated financial data are out of scope.

## Bundle Layout

- `inputs/` preserves the DOCX, extracted text, extracted media, rendered pages, provenance, and normalized preparation input.
- `analysis/` records current source evidence, assumptions, risks, and reopen triggers.
- `requirements/` contains raw-ID-preserving normalized requirements and non-goals.
- `inventories/` maps production, shared-component, persistence, test, and browser surfaces.
- `architecture/` contains the target solution plus the required C# current-state, boundary, dependency, pattern, and testability records.
- `plan/` contains execution dependencies and mandatory architecture checkpoints.
- `traceability/` maps every raw note and normalized requirement to an owner, proof, and closure path.
- `shared-prompts/` contains bounded implementation and QA handoff prompts.
- `subbundles/` contains six executable work units.
- `reviews/` contains preparation self-review, the C# architecture gate, and the execution-report skeleton.

## Recommended Execution Order

1. `subbundles/01-architecture-and-ui-design-foundation`
2. `subbundles/02-scalable-record-pickers-and-tag-consistency`
3. `subbundles/03-contact-and-relationship-dialog-flows`
4. `subbundles/04-opportunity-workspace-and-project-selection`
5. `subbundles/05-financial-insights`
6. `subbundles/06-contextual-tabs-and-final-hardening`

SB05 follows SB04 because both own `CrmHrCrmPage.razor`; do not parallelize them. SB06 starts only after all earlier subbundles have passed their progression gates.

## Dependency And Validation Map

- The operational graph, critical foundations, invalidation rules, and phase gates live in `plan/01-phase-plan.md`.
- Architecture checkpoints live in `plan/architecture-checkpoints.md`; a failing checkpoint reopens its owning subbundle before dependent work continues.
- When resuming, read this README, `traceability/01-requirement-traceability.md`, the active subbundle README, and `reviews/01-execution-report.md`. Conversation memory is not part of the execution contract.

## UI Direction

- Visual thesis: calm, dense, professional large-screen CRM with one dominant working surface.
- Content plan: compact header/navigation; searchable primary list; detail tabs; dialogs for independent create/edit flows; Financials as task-first analytics.
- Interaction thesis: restrained dialog entrance/focus, card/list selection transitions, and explicit filter/loading feedback; no ornamental motion.

## UI Target Policy

- Application routes are validated at `1800x1100` (minimum acceptable fallback `1600x900`) with no new small/medium/tablet/mobile tuning.
- Affected compound controls must also be exercised in realistic narrow dialog, list-rail, and detail-panel containers while the viewport remains large.
- The shared picker belongs to `CanDoItAll.AppComponents`, not BaseLib, so this bundle does not expand BaseLib's cross-viewport contract.

## Validation Summary

- Bundle preparation status: `Prepared; prepared-stage validator passed`
- Execution status: `Completed`
- Subbundle gate review: `CP-01 through CP-06 passed; no in-scope release blocker remains`
- Final closure gate: `Passed`
- Browser validation analytics: `Inspected at 1800x1100 across Home and the normal CRM/HR workspaces plus relevant contact, opportunity, project-picker, and financial overlays; final browser console error count was zero`
- Build and focused-test result: the final Release solution build and the post-browser Web/Integration Release builds completed with zero errors; focused component, integration, unit, privacy, Memory, and MAF Memory coverage passed either in the main run or an exact repaired-case rerun. The Home `AgentProjectionCount` regression passed `1/1`. Detailed non-inflated counts are in `reviews/01-execution-report.md`.
- Persistence result: both CRM/HR migrations are present, application startup applied migrations successfully, and the final EF model-drift check reported no model changes since the latest migration.
- Explicit non-closure baseline: the earlier broad all-suite run was not green because of unrelated pre-existing failures, including a `ProjectsCrmHrIntegrationTests` bUnit/AngleSharp mismatch. It is not represented as passing evidence.
- Security follow-up: the Release build still reports repository-wide `NU1903` warnings for `System.Security.Cryptography.Xml` `10.0.7`; dependency remediation remains required outside this bundle.
- Measured-performance follow-ups: selected-party workforce capacity/allocation loads should be profiled with production-like volume; assignment text search still uses non-sargable `ToUpper().Contains`; and the pre-existing broad `CrmHrServices.cs` aggregate remains a future decomposition candidate.
