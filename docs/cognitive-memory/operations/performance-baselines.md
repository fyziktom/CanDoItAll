# Performance Baselines

P1 adds the baseline policy; it does not introduce BenchmarkDotNet suites yet.

## Baseline Commands

Run targeted tests before and after source/recall changes:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryOperationalSettingsTests|FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
```

## Baseline Targets

| Area | Baseline target |
| --- | --- |
| External source ingestion | Large Markdown and Mermaid sources should chunk deterministically without cross-section leakage. |
| Recall | Trace persistence should retain selected candidates, source refs, context sections, and warnings without losing budget decisions. |
| Retention cleanup | Dry-run must count rows without mutation; execute must remove only eligible operational records. |
| Projection rebuild | Provider failure must leave projection rows failed and rebuildable. |

Use `dotnet-trace` or BenchmarkDotNet only after a measured slow path exists. The first production-grade performance task should add repeatable fixture generation for large manifests and recall traces rather than microbenchmarking isolated LINQ snippets.
