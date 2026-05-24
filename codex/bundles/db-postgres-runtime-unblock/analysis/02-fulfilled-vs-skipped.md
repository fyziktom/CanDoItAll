# Fulfilled vs skipped matrix

| Area | Fulfilled | Still open |
|---|---|---|
| SQLite provider package | EF SQLite package removed from infrastructure project | Run final whole-repo audit outside allowed IPFS/bundle/archive paths |
| SQLite migration project | `CanDoItAll.Migrations.Sqlite` removed from solution/diff | Ensure no stale references in scripts/docs that drive build/test |
| Typed SQLite profile model | Removed from provider/source enum and connection model | Harden quarantine tests for numeric and string legacy values |
| Snapshot runtime | `DatabaseSnapshots.cs` removed | Ensure UI/API/tests no longer call removed snapshot service |
| Data Sources UI | SQLite/snapshot buttons removed | Ensure no hidden InMemory persisted profile management leaks into UI |
| Test support | SQLite test provider removed | Convert remaining tests to PostgreSQL-backed when they validate persistence semantics |
| PostgreSQL baseline | Single baseline migration created | Add drift gate and manual real DB alignment script/checklist |
| Runtime switching | Still present | Convert normal runtime to canonical startup DB path and move hot switching to maintenance/dev-only |
| DbContext creation | Still custom switchable factory | Add pooled canonical factory and separate profile-specific admin factory |
| Automation/outbox claim | Codex only claims small concurrency improvements | Add PostgreSQL batch claim with `FOR UPDATE SKIP LOCKED` / `UPDATE ... RETURNING` proof |
| Process dispatch | Per-step static guard exists | Replace long-running guard scope with durable DB lease/claim semantics |
| Evidence hygiene | Reports written | Feature branch includes lots of bundle/proof artifacts; decide clean merge scope |
