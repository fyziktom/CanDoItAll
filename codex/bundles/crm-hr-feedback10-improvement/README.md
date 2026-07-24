# CRM-HR Feedback 10 Improvement

This initiative-profile bundle converts `feedback10.docx` and its 2026-07-24 follow-up into nine dependency-ordered work units for the CRM/HR module, shared application UI seams, public API, and operator skill.

## Profile

- `initiative` upgraded from concrete feedback because the request introduces a shared paged picker, cross-module project selection, persistence changes, chart projections, and shell-composition behavior.

## Mission

- Make CRM/HR usable with hundreds or thousands of records: consistent tag editing, safe dialog-first contact entry, scalable typed record selection, a compact opportunity workspace, honest financial insight, and contextual workspace-tab titles.
- Reduce responsibility pressure on the broad `CrmHrServices.cs`, CRM page, and Directory page by placing new reusable behavior in cohesive top-level components and query services.

## Outcome Contract

- Requested outcome: preserve the original closure for raw notes `N002` through `N010`, correct the ordinary-list and workbench-title interpretations that the follow-up invalidated, and deliver the added API/skill/seed/documentation scope.
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
- `subbundles/` contains nine executable work units.
- `reviews/` contains preparation self-review, the current C# architecture gate, and the reconciled execution report.
- `proof/` contains the reviewed source/test, API seed/repeat, bounded readback, browser, console, host, build, validator, and architecture evidence used for closure.

## Recommended Execution Order

1. `subbundles/01-architecture-and-ui-design-foundation`
2. `subbundles/02-scalable-record-pickers-and-tag-consistency`
3. `subbundles/03-contact-and-relationship-dialog-flows`
4. `subbundles/04-opportunity-workspace-and-project-selection`
5. `subbundles/05-financial-insights`
6. `subbundles/06-contextual-tabs-and-final-hardening`
7. `subbundles/07-directory-workforce-catalogs-and-dialogs`
8. `subbundles/08-crmhr-http-api-and-skill`
9. `subbundles/09-api-seeded-scenarios-docs-and-closure`

SB05 follows SB04 because both own `CrmHrCrmPage.razor`; do not parallelize them. SB06 starts only after all earlier subbundles have passed their progression gates.
SB07 and SB08 are parallel-safe by source ownership. SB09 depends on SB08, and final closure depends on both SB07 and SB09.

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
- Validator result: `Prepared and completed initiative-profile stages pass`
- Execution status: `Completed`
- Subbundle gate review: `CP-01 through CP-09 pass`
- Final closure gate: `Pass`
- Browser validation analytics: inspected `1800x1100` follow-up states show full-width Directory and Workforce catalogues, real bounded scrolling, second-page navigation, usable Amina/Lucas dialogs, populated Recruiting context, and a final console with `0` errors and `0` warnings.
- Build and focused-test result: the final Release solution build exited `0` with `0 errors`, `165 warnings` in `31.39s`; feature UI passed `37/37`, the dedicated recruiting-race regression passed `1/1`, focused CRM-HR API passed `2/2`, and the broader CRM-HR integration selection passed `35/35`. Exact commands and elapsed times are in `proof/final-validation.md`.
- Seed and runtime result: the public-API-only operator produced `29` demo parties inside `78` total, `32` workforce records, and `8` applications across Applied, Screening, Interviewing, Offer, Hired, Rejected, and Withdrawn. Its immediate reconciliation performed zero creates/writes/replacements/conversions and reused every tracked identity. The final port `5032` Release host returned HTTP `200`, reported totals `78/32/8`, had empty stderr, and contained no inspected server error pattern.
- Persistence result: both CRM/HR migrations are present, application startup applied migrations successfully, and the final EF model-drift check reported no model changes since the latest migration.
- Explicit repository baseline: the broader all-unit run is not claimed green. It was diagnostically stopped after unrelated existing workflow snapshot, seed-version/hygiene, stale in-memory fixture, and secret-scan failures. The affected focused suites above are the closure evidence.
- Security follow-up: the Release build still reports repository-wide `NU1903` warnings for `System.Security.Cryptography.Xml` `10.0.7`; dependency remediation remains required outside this bundle.
- Measured-performance follow-ups: selected-party workforce capacity/allocation loads should be profiled with production-like volume; assignment text search still uses non-sargable `ToUpper().Contains`; and the pre-existing broad `CrmHrServices.cs` aggregate remains a future decomposition candidate.
- Durable closure evidence: `proof/README.md` indexes the inspected screenshot hashes, exact seed/repeat/readback facts, race regression, host logs/probes, build/tests, skill synchronization, and validator results.
