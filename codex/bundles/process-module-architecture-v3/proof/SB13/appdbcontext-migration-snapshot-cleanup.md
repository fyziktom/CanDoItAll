# SB13 AppDbContext Migration Snapshot Cleanup

The SB13 browser fixture starts the real web application against a fresh PostgreSQL profile. During validation, app startup failed before route proof because EF detected pending AppDbContext model changes after the legacy Process module was removed from active composition.

`ProcessModuleArchitectureV3RuntimePersistence` synchronizes the active AppDbContext migration snapshot with the architecture-v3 removal of legacy `Processes_*` AppDbContext entities. Its `Up` path drops the legacy Process tables that are no longer part of the active AppDbContext model. It does not wire the new `ProcessPersistenceDbContext` into the deployed app; SB08 proof explicitly deferred runtime process persistence deployment wiring to later integration subbundles.

Validation:

- `bundle://proof/SB13/ef-pending-model-check.txt` reports: `No changes have been made to the model since the last migration.`
- `bundle://proof/SB13/test-playwright-process-shell.txt` proves the full web host migrates a fresh PostgreSQL profile and renders the Process shell routes.

Risk:

- This migration is destructive for legacy AppDbContext `Processes_*` tables by design. The old module was already archived in SB01 and removed in SB02; legacy runtime history remains read-only through the SB12 compatibility plan until a future migration path is explicitly implemented.
