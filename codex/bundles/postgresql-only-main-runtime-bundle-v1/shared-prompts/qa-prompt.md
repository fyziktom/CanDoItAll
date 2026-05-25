# Shared QA prompt

Review the completed subbundle for architectural correctness.

Check:

1. Was the subbundle scope completed without unrelated changes?
2. Are there hidden SQLite fallbacks?
3. Did any test get weakened?
4. Are unsupported legacy SQLite profiles handled clearly?
5. Are PostgreSQL assumptions explicit?
6. Is CanDoItAll.IPFS untouched?
7. Does the proof manifest match actual code?
8. Are all claims backed by build/test/audit evidence?

Run or verify:

```powershell
rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|snapshotcache|ipfssnapshot" src tests docs
dotnet build .\CanDoItAll.slnx
```

For SB06 and later, also verify PostgreSQL-backed concurrency tests.
