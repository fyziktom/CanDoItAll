# M01 semantic invariants

| Invariant | Implementation | Evidence |
|---|---|---|
| Historic plans use their historic canonical hash | Explicit `LegacyV1` branch omits host/profile fields introduced after the fixture | Exact parent JSON/hash fixture loads through V1 verification |
| Missing capability fields never mean empty requirements | Verified V1 plans are persisted as `NeedsRecompile`; ambiguous unversioned plans fail | Boundary/property negative tests and typed exception assertions |
| Current plans retain tamper detection | V2 remains the default/current hasher and requires consistent executable metadata | Current valid-load and payload-tamper tests |
| Tampering cannot write migration metadata | Hash verification precedes legacy classification/persistence | Tampered V1 test asserts metadata remains null/`Unknown` |
| Migration is transactional and restart-safe | EF migration performs classification before the non-null alteration; store normalization uses one `SaveChangesAsync` | PostgreSQL migrate/repeat/restart/down test and InMemory restart test |
| Rollback evidence is preserved | Neither runtime normalization nor SQL migration rewrites payload/hash | Unit and PostgreSQL down-migration assertions compare exact payload/hash |
| Unknown database rows fail closed | Migration assigns `Unknown`; mapper accepts executable V2 or verified V1 only | Missing-version boundary/property tests |
