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
| `R013` architecture | `architecture/`, `reviews/csharp-architecture-gate.md` | SB01-SB06 | Per-checkpoint boundary/no-partial/build proof | CodeAnalytics evidence gap remains explicit. |
| `R014` large-screen UI | `design/ui-proposals/`, each subbundle | SB01-SB06 | `1800x1100` normal/open-overlay review | No small/medium application scope. |
| `R015` closure quality | `reviews/01-execution-report.md` | SB06 | Positive/adversarial proof, anti-stub audit, completed validator | Reopen weak owners. |
