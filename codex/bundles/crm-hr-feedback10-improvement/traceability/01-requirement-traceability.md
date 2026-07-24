# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001` / `R001` CRM heading | `requirements/01-normalized-requirements.md` | SB06 closure audit | Source-preservation and raw-note table | Informational; no feature implementation. |
| `N002` / `R002` TagEditor consistency | `subbundles/02-scalable-record-pickers-and-tag-consistency` | SB02 | Component round trip, source audit, Directory browser proof | Includes tag filters and mutable tag collections. |
| `N003` / `R003` stable callbacks | `subbundles/03-contact-and-relationship-dialog-flows` | SB03 | Exact add/remove regression plus adjacent-editor tests | Bounds checks alone do not close it. |
| `N003` / `R004` contact wizard/tags | `subbundles/03-contact-and-relationship-dialog-flows` | SB03 | Wizard state tests, persistence/migration round trip, browser overlay | Primary contacts remain dedicated fields. |
| `N004` / `R005` reusable browser | `subbundles/01-architecture-and-ui-design-foundation` | SB01 | Fake-loader component tests, error/stale/page proof | No full-list fallback. |
| `N004` / `R006` scale/scopes | `subbundles/02-scalable-record-pickers-and-tag-consistency` | SB02 | >1,000-record stable paging/search/tag/type integration proof | Search/tags source-side. |
| `N004`,`N005` / `R007` adoption | `subbundles/02-scalable-record-pickers-and-tag-consistency` | SB02 | Selector/list source audit and real form browser flows | Finite enum dropdowns excluded. |
| `N006` / `R008` opportunity pipeline | `subbundles/04-opportunity-workspace-and-project-selection` | SB04 | Bounded query tests, compact browser proof, currency negative | At most two filter rows. |
| `N007` / `R009` opportunity dialogs | `subbundles/04-opportunity-workspace-and-project-selection` | SB04 | Create/detail/edit component and Playwright flows | Cancel must not mutate. |
| `N008` / `R010` project picker | `subbundles/04-opportunity-workspace-and-project-selection` | SB04 | Projects query paging, select/clear/save/reload/missing-id proof | Projects owns query. |
| `N009` / `R011` Financials | `subbundles/05-financial-insights` | SB05 | Mixed-currency/Won-history projection tests and chart DOM/screenshot | Purchase/invoice data remains unavailable. |
| `N010` / `R012` contextual tabs | `subbundles/06-contextual-tabs-and-final-hardening` | SB06 | Route theory, layout identity, six-route browser proof | Main nav unchanged. |
| `R013` architecture | `architecture/`, `reviews/csharp-architecture-gate.md` | SB01-SB09 | Per-checkpoint boundary/no-partial/build proof | CP-01 through CP-09 pass. No project-reference change or direct Web persistence was introduced; the CodeAnalytics evidence gap remains explicit. |
| `R014` large-screen UI | `design/ui-proposals/`, UI-owning subbundles | SB01-SB07, SB09 | `1800x1100` normal/open-overlay review | Historical SB01-SB06 and inspected follow-up SB07/SB09 proof pass. No small/medium application scope. |
| `R015` closure quality | `reviews/01-execution-report.md`, `proof/README.md` | SB06, SB09 | Positive/adversarial proof, anti-stub audit, completed validator | Semantic positive, repeat-idempotency, populated-render race negative, anti-stub, host, affected regression, and completed-validator evidence pass. |
| Follow-up / `R016` catalogue composition | `subbundles/07-directory-workforce-catalogs-and-dialogs` | SB07 | Shared-browser scroll contract, component tests, populated Directory/Workforce screenshots | Reopens the over-broad prior `N005` / `R007` closure claim. |
| Follow-up / `R017` dialog and title clarity | `subbundles/07-directory-workforce-catalogs-and-dialogs` | SB07 | Route/dialog synchronization tests, title theory, open-overlay browser proof | Reopens the weak prior `N010` / `R012` title interpretation. |
| Follow-up / `R018` CRM-HR API and skill | `subbundles/08-crmhr-http-api-and-skill` | SB08 | HTTP positive/negative tests, skill validation, active-root hash match | New scope; not hidden in feedback10. |
| Follow-up / `R019` API-created scenarios | `subbundles/09-api-seeded-scenarios-docs-and-closure` | SB09 | Repeatable HTTP transcript, identity-idempotency check, populated browser proof | Direct DB/startup seeding is forbidden. |
| Follow-up / `R020` documentation and closure | `subbundles/09-api-seeded-scenarios-docs-and-closure` | SB09 | Docs/source audit, architecture/performance gates, completed validator, 5032 health | Final closure depends on SB07 and SB09. |

## Follow-Up Closure Status

| Requirement | Closure state | Verified evidence | Remaining closure path |
| --- | --- | --- | --- |
| `R016` | `Solved` | Typed bounded result scrolling, full-width catalogue composition, `37/37` focused component proof, actual second-page navigation, and inspected populated Directory/Workforce states are recorded in the SB07 completion record and `bundle://proof/SB07/browser-normal-and-dialog-review.md`. | Reopen only on paging, width, first-viewport, scroll-owner, or fallback-list regression. |
| `R017` | `Solved` | Controlled record dialogs, stale-close generation protection, contextual route titles, focused tests, and inspected Amina/Lucas open-dialog states pass. | Reopen only on route/dialog desynchronization, stale reopening, clipped actions, unusable tab scrolling, or ambiguous titles. |
| `R018` | `Solved` | Thin CRM-HR Web transport, real-host positive/negative tests, validated/synchronized skill files, and affected/full builds are cited by `bundle://subbundles/08-crmhr-http-api-and-skill/README.md`. | Reopen only on contract, authorization inheritance, privacy, skill-sync, or canonical-service drift. |
| `R019` | `Solved` | `bundle://proof/SB09/seed-first-run.md`, `bundle://proof/SB09/seed-repeat-run.md`, and `bundle://proof/SB09/api-readback.md` prove the public-API-only scenario, stable identity reuse with zero repeat writes, and bounded populated readback. | Reopen on duplicate identity, direct persistence/startup seeding, missing stage diversity, or API/UI disagreement. |
| `R020` | `Solved` | Module/API/skill/bundle documentation agrees with the final `0`-error build, affected green suites, inspected populated UI/console, architecture/performance gate, healthy port `5032` host, and completed validator. | Reopen on documentation drift, failed affected gate, unhealthy final host, architecture violation, or missing durable proof. |

All follow-up closure proof is indexed in `bundle://proof/README.md`. The existing `NU1903` advisory, unrelated all-unit repository debt, and unavailable CodeAnalytics/Components transports remain explicit residual risks rather than hidden closure blockers.
