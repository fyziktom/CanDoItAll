# 06-persistence-diagnostics-and-service-refactor

## Status

- `Completed`

## Objective

Clean up the implementation shape after behavior is covered: split the monolithic quality service file, harden diagnostics/logging, and verify persistence/migration projects.

## Success Criteria

- `CognitiveMemoryQualityServices.cs` is split into focused files or classes with clear responsibility boundaries.
- Public contracts remain stable unless an earlier subbundle intentionally changed them.
- DI registration still resolves all quality services.
- Diagnostics include actionable counts/warnings and do not leak sensitive text.
- Logs, if added, include useful identifiers/status/mode but mask source content and restricted data.
- SQLite and PostgreSQL migration projects build.

## Covered Inputs

- H-02, H-13, H-14, H-16.

## Prerequisites

- Subbundles 01 through 05 complete, or a documented decision that a behavior-owning subbundle does not need code changes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityDiagnosticsService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryClusterPlanner.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamConsolidationService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryDreamValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryAggregateMemoryApplicator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryRecallSynthesisService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryReferenceResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualitySupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj`

## Deliverables

- Focused service files and internal helper files under the existing `Quality` folder.
- Updated DI registration if constructor dependencies change.
- Diagnostics/logging improvements with masked sensitive state.
- Migration project build proof.
- Optional follow-up schema migration only when required by earlier subbundles.

## Dependency Impact

- This phase reduces long-term maintenance risk after correctness is covered. It should not invalidate earlier behavior proof; if it does, reopen the owning behavior subbundle.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Confirm all behavior gates from earlier subbundles are passing.
2. Split implementation by service responsibility with minimal public API churn.
3. Move shared support loading/text helpers into internal focused files.
4. Update DI registrations only where required.
5. Add diagnostics/logging tests or review assertions where feasible.
6. Build CognitiveMemory and migration projects.

## Scope Exceptions

- Do not perform broad namespace or project restructuring.
- Do not regenerate migrations unless schema changes require it.

## Do Not Do

- Do not refactor before behavior tests exist.
- Do not introduce interfaces with one trivial implementation unless they define a real boundary or enable required tests.
- Do not log raw source content, restricted locators, or sensitive summaries.

## Acceptance Checklist

- Code is split by responsibility and remains readable.
- No accidental public contract drift.
- DI registration tests or existing startup-related tests pass.
- Module and migration projects build.

## Proof Required

- `dotnet build src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore -m:1`
- `dotnet build src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --no-restore -m:1`
- `dotnet build src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --no-restore -m:1`
- Targeted unit tests for all changed quality services.

## Browser Validation Logging

- N/A. This subbundle is implementation/persistence cleanup only.

## Progression Gate

- Subbundle 07 may close only after the refactor builds cleanly and earlier behavioral tests still pass.

## Suggested Agent Prompt

```text
Implement subbundle 06 only. Refactor for maintainability after behavior proof exists. Keep contracts stable, logs masked, and record build/test proof.
```
