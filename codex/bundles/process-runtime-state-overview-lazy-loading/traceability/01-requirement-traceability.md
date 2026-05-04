# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001, R001 | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | `subbundles/01-runtime-state-overview-service` | Integration test for active/blocked/failed counts and UI badge browser check | Corrects misleading active count. |
| N002, R002 | `requirements/01-normalized-requirements.md` | `subbundles/01-runtime-state-overview-service` | Browser screenshot/evaluate against processes page | UI must show separate badges. |
| N003, N004, R003, R004 | `architecture/01-target-solution.md` | `subbundles/01-runtime-state-overview-service` | Code review plus test proving projection derives from existing services/queries | No second source of truth. |
| N006, N007, R005, R006 | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `subbundles/02-lazy-run-detail-loading` | Focused test or code proof plus browser page-open check | Full detail loading should be delayed. |
| N005, R007, R008 | `requirements/01-normalized-requirements.md` | `subbundles/03-blocked-run-stop-action` | Integration test for stop operation and browser UI check | Stop means explicit cancellation, not deletion. |
| R009, R010 | `reviews/01-execution-report.md` | `subbundles/04-validation-and-proof` | `dotnet test`, `dotnet build`, Playwright/browser proof or blocker | Closure proof. |
