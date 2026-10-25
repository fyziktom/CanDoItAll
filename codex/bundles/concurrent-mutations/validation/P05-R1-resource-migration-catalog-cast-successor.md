# Resource migration catalog-observation correction

The frozen Resource Integration lane discovered and executed 173 rows: 172 passed and one failed. `ResourceMetadataMigrationTests.Drift_partial_installation_lock_refusal_and_down_leave_history_and_retained_bytes_unchanged` failed in its first `SchemaAsync` observation (line 140), before `UpgradeAsync` for that arrangement. PostgreSQL returned `42725`, `operator is not unique: unknown || "char"`, at SQL position 1015. This is an observation-query defect; this failed run does not prove the drift, partial-installation, lock-refusal or irreversible-Down arrangements.

The successor changes only `relkind` to `relkind::text` in the schema fingerprint's relation expression. PostgreSQL documents `pg_class.relkind` as its catalog character field and resolves operators from operand types; explicitly selecting text removes this overload ambiguity. [PostgreSQL 18 pg_class](https://www.postgresql.org/docs/18/catalog-pg-class.html), [operator type resolution](https://www.postgresql.org/docs/18/typeconv-oper.html).

The relation kind, row-security flags, columns, constraints, indexes and triggers remain in the exact schema observation. All assertions, cleanup ownership, retained Resource/Search comparisons and three Facts remain unchanged. No product, generated migration, model, namespace, binding or test-selection change is included.

The failed source F91A42B47EFEE72B0E193C26D8B6D29DD93062A9EB7D15DD06F7AF9D6FEA3075 (22,914 bytes), its cleanup overlay and original inventories remain immutable. The successor is 39C8748DF4121DC82298C49C355439858BD840AD97BEAC2350C97A97C5797473 (22,920 bytes). Reversing the sole six-byte `::text` insertion reproduces the predecessor exactly; source count remains three Facts, 346 ordinary union rows and four separate browser rows.

Failure evidence remains in MAIN's owned `resource-deletion-validation`: TRX E347C4367CF7EF8AA8F65FA3D603852B7D7E8B82F24BE3C06B34C4F83FC78AC5 and result JSON 401C0D1C21ABF72EF226C1AFA8EF73A33594EAED1CF50AC002951CCFD8002F8F. The source author did not execute SQL, tests, builds or scans and did not modify MAIN. Parent-owned standard Release rebuild, exact discovery/execution and final portability-static enforcement remain required. Existing failed artifacts and runner V3 identities must be retained; any next candidate requires an explicit source-binding successor.

UI peer independently verified the exact one-token delta against the preserved predecessor and reported source PASS. This is not execution proof.
