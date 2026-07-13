# SB04 Semantic Invariants

## Invariant MTE-SB04-E2E

- Invariant ID: `MTE-SB04-E2E`
- Source raw note: The simple Calculator multiteam development flow should pass end to end on 5032 with updated templates loaded.
- Expected behavior: A fresh Calculator run completes without the prior false escalation loop, focused tests and full build pass, 5032 runs in Development, the database is `candoitall_development`, and live launch readiness reports all executable steps staffed.
- Disallowed shallow implementation: Reporting success from local files only without a running 5032 instance, live launch check, database proof, or process run proof.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first.txt` records the closure validation failure path that required direct CLI proof after the managed queue wedged.
- Passing test: `bundle://proof/SB04/transcripts/passing.txt` records focused tests, full build, runtime/database checks, and live launch/check passing.
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs` with hash `41D23EBD5C6398A9F0200E20F08D04A7BF703B095FDA42925CB6217205604F42`.
- Production assertions: Root proof run `170c9b2b-47da-4a21-a7bc-f57e90aff59c` completed and QA retry execution `81968edb-ad84-4bdf-b43d-fa93f43afeb5` selected `quality-accepted`.
- Red-team negative case: A stale template or missing HR assignment would make live launch/check return a readiness finding other than `process.launch.readiness_ok`.
- Downstream dependency check: The final running app on 5032 uses the repaired template loader path and is ready for the user's next E2E run.
