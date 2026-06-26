# Post-SB34 Process Estimate Historical Cost Repair Proof Manifest

## Scope

Repaired project-structure process-start price estimation so the HR/staffing launch dialog prefers actual historical cost from completed runs of the same process definition before falling back to current provider model price lists.

The repair is intentionally narrow:

- Adds a typed `IProcessHistoricalRunCostReader` contract in the Process application layer.
- Adds an EF-backed reader that finds completed historical process runs by `ProcessDefinitionId`, includes descendant run IDs for root-run samples, and averages actual usage cost per completed process run.
- Wires the project-structure process-start estimate to read historical cost during launch preview.
- Keeps the existing provider-price estimate as an explicit fallback when historical completed runs have no resolvable actual usage cost.

## Root Cause

`ProjectStructureProcessStartEstimateCalculator` used fixed per-assignment token constants (`100,000` input and `25,000` output tokens) multiplied by selected provider model prices. That made the estimate repeat for the same process/role/model shape even after real process runs produced actual usage costs.

The manager/runtime price path was already reading normalized process usage telemetry. The preflight estimate path simply had no dependency on historical process run data.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProcessHistoricalRunCostEstimate` | `EfProcessHistoricalRunCostReader` joins `process_runtime_states` to `process_instance_plans`, then reads `IProcessRuntimeUsageTelemetryReader` observations for matching run IDs. | `ProjectStructureProcessStartEstimateCalculator` and the project-structure process launch preview dialog. | Created per launch preview; not persisted. Uses existing runtime state, plan, and usage telemetry records. | `repo://tests/CanDoItAll.Tests.Unit/EfProcessHistoricalRunCostReaderTests.cs` verifies failed runs are ignored and empty history does not call usage telemetry. |
| Historical estimate source/confidence labels | `ProjectStructureProcessStartEstimateCalculator` when historical actual costs exist. | HR/staffing assignment dialog and canvas dialog estimate display. | Rendered with the existing `ProjectStructureProcessEstimateSummary` UI contract. | `repo://tests/CanDoItAll.Tests.Unit/ProjectStructureProcessStartEstimateCalculatorTests.cs` verifies provider fallback remains explicit when historical runs lack actual cost. |

## Proof Files

- `changed-file-hashes.txt`
- `semantic-invariants.md`
- `transcripts/focused-unit-tests.txt`
- `transcripts/component-page-render-smoke.txt`
- `transcripts/git-diff-check.txt`
- `transcripts/validate-bundle-prepared.txt`

## Source Assertions

- `repo://src/CanDoItAll.Processes.Application/ProcessHistoricalRunCostContracts.cs` defines the typed historical cost query/result contract.
- `repo://src/CanDoItAll.Processes.Persistence/EfProcessHistoricalRunCostReader.cs` filters completed runs by `ProcessDefinitionId`, includes descendant run IDs for root process samples, and averages positive actual usage cost per completed run.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs` loads historical cost during process-start preview before creating the dialog estimate.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureProcessStartEstimateCalculator.cs` prefers historical actual-cost averages and falls back to provider model pricing with an explicit missing-history-cost summary.

## Validation Result

Focused unit validation passed 6/6:

```text
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectStructureProcessStartEstimateCalculatorTests|FullyQualifiedName~EfProcessHistoricalRunCostReaderTests"
```

Full project-structure page render smoke validation passed 1/1:

```text
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName=CanDoItAll.Tests.Components.ProjectStructurePageDatabaseSwitchTests.Missing_project_routes_render_a_safe_structure_recovery_state"
```

`git diff --check` passed with no whitespace errors.

Prepared-stage bundle validation passed:

```text
python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\process-module-architecture-v3 --stage prepared --repo-root . --bundle-root codex\bundles\process-module-architecture-v3
```
