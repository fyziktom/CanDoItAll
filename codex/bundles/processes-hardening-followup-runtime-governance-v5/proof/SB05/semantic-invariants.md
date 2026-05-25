# SB05 Semantic Invariants

## Invariant SB05-INV-001

- Invariant ID: SB05-INV-001
- Source raw note: N001, N005
- Expected behavior: Artifact projection lineage has a stable identity hash used for dedupe and PostgreSQL uniqueness.
- Disallowed shallow implementation: Display keys alone cannot prevent duplicate projected artifacts when lineage content is identical.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first.txt
- Passing test: bundle://proof/SB05/transcripts/passing.txt
- Changed source files: src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs, src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs, src/CanDoItAll.Migrations.PostgreSql/Migrations/20260525184500_ProcessRuntimeGovernanceV5.cs, tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- Production assertions: bundle://proof/SB05/transcripts/source-assertions.txt cites production paths and focused tests.
- Red-team negative case: Display keys alone cannot prevent duplicate projected artifacts when lineage content is identical.
- Downstream dependency check: reviews/01-execution-report.md gate row for SB05 closes downstream dependency checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB05-INV-001 governed behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs and dotnet test proof | Closed by bundle://proof/SB05/transcripts/passing.txt | Red-team rejection in bundle://proof/SB05/transcripts/failing-first.txt |
