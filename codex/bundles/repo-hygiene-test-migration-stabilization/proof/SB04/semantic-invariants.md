# SB04 Semantic Invariants

- Invariant ID: `SB04-EF-MODEL-ISOLATION`
- Source raw note: RH-007 and RH-008 required deterministic database migration and test isolation proof.
- Expected behavior: AppDbContext model registry changes are isolated in tests, PostgreSQL model snapshot is clean, and retained CognitiveMemory tables remain represented by the current model.
- Disallowed shallow implementation: globally suppressing pending model warnings or generating schema churn while EF reports no legitimate model difference.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB04/transcripts/passing.txt`
- Changed source files: `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`, `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260707110549_IncludeCognitiveMemoryModuleModel.cs`
- Production assertions: composition includes the CognitiveMemory module assembly and migration adoption recognizes the restored snapshot migration.
- Red-team negative case: `bundle://proof/SB04/transcripts/anti-stub.txt` verifies no global pending-model suppression was used.
- Downstream dependency check: SB05 full unit proof passes after the database order-specific and EF checks.

