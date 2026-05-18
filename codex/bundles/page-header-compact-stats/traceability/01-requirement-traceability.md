# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 processes reference pattern | `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md` | `subbundles/01-01-shared-compact-header-primitives` | `/processes` screenshot and tooltip proof | Processes remains the baseline and gets shared tooltip policy. |
| N002 / R003 large page stat cards | `analysis/01-current-state.md` | `subbundles/02-02-page-and-tab-stat-migration` | Large-screen screenshots of representative migrated pages | Top-level production pages are prioritized. |
| N003 / R006 icon-only header actions | `architecture/01-target-solution.md` | `subbundles/01-01-shared-compact-header-primitives`, `subbundles/02-02-page-and-tab-stat-migration` | Build plus header screenshots and tooltip checks | Convert header actions touched by the migration. |
| N004-N005 / R002 tooltip detail and 2s delay | `architecture/01-target-solution.md` | `subbundles/01-01-shared-compact-header-primitives` | Playwright hover checks wait less than 2s and more than 2s where practical | Shared primitive owns timing. |
| N006 / R002 shared maintainable source | `architecture/01-target-solution.md` | `subbundles/01-01-shared-compact-header-primitives` | File diff shows BaseLib shared components used by pages | Avoid repeating raw tooltip wrappers everywhere. |
| N007 / R004-R005 CRM tabs and subpages | `analysis/01-current-state.md` | `subbundles/02-02-page-and-tab-stat-migration` | CRM-HR route screenshots plus inventory grep | CRM module is explicit priority. |
| N008 / R007 large-screen screenshot proof | `plan/01-phase-plan.md`, `reviews/01-execution-report.md` | `subbundles/03-03-large-screen-browser-proof` | Build/test command output, screenshots, browser analytics rows | Medium/mobile tuning remains out of scope. |
