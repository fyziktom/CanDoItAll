# Source Artifacts

- `evidence/targeted-failing-tests.txt`: targeted VSTest/xUnit probe for known failing unit-test clusters. Current result: 10 failures, 152 passed, 162 total.
- `evidence/database-runtime-switch-test.txt`: isolated database runtime-switch test. Current result: 1 passed.
- `evidence/ef-pending-model-check.txt`: EF Core pending-model check for PostgreSQL migrations. Current result: build succeeded; no pending model changes; `dotnet-ef` is 10.0.3 while EF runtime is 10.0.4.
- Historical source: `codex/bundles/filesystem-agent-tools/proof/full-unit-test.txt` showed the same hygiene/process failures plus a prior `PendingModelChangesWarning` during a broad unit-suite run.
- Current platform evidence: .NET SDK `10.0.204`, `global.json` pins `10.0.200` with latest patch roll-forward, test project uses xUnit with `Microsoft.NET.Test.Sdk`.
