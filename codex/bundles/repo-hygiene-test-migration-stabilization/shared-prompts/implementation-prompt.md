# Implementation Prompt

Implement this bundle phase by phase.

Start by rereading `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md`, and the current subbundle README. Do not broaden the scope into unrelated test cleanup.

Rules:

- Capture failing-first proof for each repaired failure cluster before editing it, unless the evidence file already captures the exact failure and no code changed since.
- Prefer fixing obsolete fixtures or exact production defects over weakening assertions.
- Do not add an EF migration unless `dotnet ef migrations has-pending-model-changes` fails after DB isolation fixes.
- Preserve repository hygiene test value; no broad `codex/` or `tests/` exclusion unless the bundle explicitly records why that source is intentionally durable.
- After each subbundle, update `reviews/01-execution-report.md` with command transcripts and gate status.

Required final proof:

```powershell
dotnet build tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --filter "<repaired targeted filters>"
dotnet ef migrations has-pending-model-changes --project src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context CanDoItAll.Infrastructure.Persistence.AppDbContext
```
