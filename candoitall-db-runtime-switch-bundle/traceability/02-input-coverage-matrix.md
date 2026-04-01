# Input Coverage Matrix

| Raw note ID | Raw wording / meaning | Normalized requirement(s) | Owning subbundle(s) | Planned proof | Exception / narrowing |
| --- | --- | --- | --- | --- | --- |
| `N-01` | Allow selecting the database to work with | `RQ-001`, `RQ-003`, `RQ-007`, `RQ-010`, `RQ-011` | `02`, `07` | Component + browser UX proof | None |
| `N-02` | Allow switching the database during runtime | `RQ-004`, `RQ-005`, `RQ-006`, `RQ-022` | `03`, `06`, `08` | Integration + Playwright multi-tab switch | None |
| `N-03` | Add SQLite support | `RQ-010`, `RQ-012`, `RQ-015`, `RQ-016` | `02`, `04`, `07`, `08` | Integration create/open/import + migration proof | Clarified as “first-class runtime/profile support,” because startup-time SQLite already exists |
| `N-04` | Analyze existing codebase and DB-related things | Analysis docs + inventories | Entire bundle | Static repo analysis evidence | Done in bundle prep |
| `N-05` | Primarily PostgreSQL now / loading automatically during start / cannot switch | `RQ-002`, `RQ-004`, `RQ-015` | `02`, `03`, `04` | Startup resolution tests + runtime switch tests | Repo analysis shows SQLite already partially exists |
| `N-06` | Design architecture changes that lead to goals | Architecture docs | Entire bundle | Bundle review + validator | Done in bundle prep |
| `N-07` | Create detailed plan and subbundles | Plan + subbundle READMEs | Entire bundle | Prepared-stage validator | Done in bundle prep |
| `N-08` | Validate whole bundle so Codex can implement and validate fluently | `RQ-023` + reviews | `01`, `08` | Bundle validator + self-review | Done in bundle prep, runtime proof deferred |
| `N-09` | Codex sometimes fakes validations or skips subbundles | `RQ-023` | `01`, `08` | Stop-the-line checklist + execution report rules | None |
| `N-10` | Critical unit tests and E2E tests | `RQ-019`, `RQ-020`, `RQ-021`, `RQ-022` | `01`-`08` | All test layers | None |
| `N-11` | Switching DB at runtime must reload all running modules/services with new data | `RQ-004`, `RQ-005`, `RQ-006` | `03`, `06`, `08` | Multi-tab/circuit Playwright proof + integration tests | None |
| `N-12` | Start with last setup of DB and show continue/switch info modal | `RQ-002`, `RQ-003` | `02`, `07` | Component + browser proof | None |
| `N-13` | SQLite source can be file dialog/path/AppData existing DB/IPFS | `RQ-010`, `RQ-014` | `02`, `07`, `08` | Integration/UI proof | Clarified as local materialization for IPFS-backed sources |
| `N-14` | PostgreSQL source can be localhost/process/docker or remote server | `RQ-011` | `02`, `07`, `08` | Real PostgreSQL proof | Docker lifecycle automation is not required |
| `N-15` | Create new DB for both SQLite and PostgreSQL | `RQ-012` | `03`, `07`, `08` | Integration create tests | None |
| `N-16` | Optional clone of all data / snapshot branch behavior | `RQ-013` | `08` | Integration clone tests | Snapshot includes storage, not just DB rows |
| `N-17` | IPFS node can pin versions for rollback/versioning | `RQ-014` | `08` | Fake-server tests + real-node proof if available | Live mutable DB on IPFS is narrowed out of v1 |
| `N-18` | Final QA/architect validation before final zip | Reviews + execution report seed | Entire bundle | Self-review + prepared-stage validator | Done in bundle prep; runtime closure left to execution phase |
