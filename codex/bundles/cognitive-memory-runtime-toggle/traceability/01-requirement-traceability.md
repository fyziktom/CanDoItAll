# Requirement Traceability

| Requirement | Raw notes | Owning subbundle | Planned proof | Bundle files |
| --- | --- | --- | --- | --- |
| `R001` | `N003`, `N004` | `SB01` | Settings unit test and source assertion | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` |
| `R002` | `N003`, `N004` | `SB01` | Component/API compile and targeted tests | `subbundles/01-global-runtime-setting-and-api-contract/README.md` |
| `R003` | `N001`, `N002`, `N005`, `N006` | `SB02` | Unit test proves disabled contributor skips without recall | `subbundles/02-skip-cognitive-memory-integration-points/README.md` |
| `R004` | `N005`, `N006` | `SB02` | Unit/source proof for executor disabled payloads | `subbundles/02-skip-cognitive-memory-integration-points/README.md` |
| `R005` | `N005`, `N006` | `SB02` | Unit test proves runner returns not executed without downstream calls | `subbundles/02-skip-cognitive-memory-integration-points/README.md` |
| `R006` | `N002`, `N006` | `SB02` | Existing missing-project-scope test remains enabled-mode failure | `subbundles/02-skip-cognitive-memory-integration-points/README.md` |
| `R007` | `N003`, `N007` | `SB01`, `SB03` | EF migrations and database update transcript | `subbundles/01-global-runtime-setting-and-api-contract/README.md`, `subbundles/03-validation-and-clean-development-database/README.md` |
| `R008` | `N007` | `SB03` | PostgreSQL reset and migration transcript | `subbundles/03-validation-and-clean-development-database/README.md` |

## Raw Note Closure Plan

| Raw note | Owning requirement | Planned closure |
| --- | --- | --- |
| `N001` | `R003`, `R004`, `R005` | Solved when disabled guards cover optional integrations. |
| `N002` | `R003`, `R006` | Solved when disabled contributor skips and enabled missing-scope behavior remains explicit. |
| `N003` | `R001`, `R002`, `R007` | Solved when persisted setting exists and is visible in UI/API. |
| `N004` | `R001`, `R002` | Solved when settings are read per call from DB. |
| `N005` | `R003`, `R004`, `R005` | Solved for optional cross-feature integrations; direct CM management endpoints are a documented exception. |
| `N006` | `R003`, `R004`, `R005`, `R006` | Solved when disabled returns skip/no-op rather than failure. |
| `N007` | `R008` | Solved when `candoitall_development` is clean and migrated. |
