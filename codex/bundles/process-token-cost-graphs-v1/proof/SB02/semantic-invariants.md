# SB02 Semantic Invariants

## SB02-I01 Completed Priced Runs Remain In Windowed History

- Raw note: "Then, when process finished and I refreshed live processes page and selected for example 1 day history, I cannot see prices graph."
- Expected behavior: completed runs updated inside the selected one-day window are included in live observation history, and their persisted cost contributes to stats and money series.
- Disallowed shallow implementation: only include currently active runs, causing completed runs to disappear after refresh.
- Failing-first proof: the component test marks a run completed with actual cost and queries one-day history after refresh; the old active-only filter would return no priced run.
- Passing proof: `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Red-team negative case: a completed priced run outside the selected process/run scope is excluded by the scoped query path.
- Downstream dependency check: SB03 process and run graph tabs consume this same observation service.

## SB02-I02 Graph Query Scope Is Typed And Bounded

- Raw note: graph loading is large and must be scoped by selected process or selected run.
- Expected behavior: live observation query carries typed `ProcessDefinitionId` and `ProcessRunId` values, and history windows bound the run set except explicit `All`.
- Disallowed shallow implementation: add UI filters but let the service still query all process runs.
- Failing-first proof: lazy graph component tests assert the selected run graph snapshot loads only after selecting the run graph tab and records the selected run id.
- Passing proof: `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Red-team negative case: a process-scoped query builds `processRunIdsForHistory` from observed runs for the selected definition rather than every run.
- Downstream dependency check: SB03 all-runs and selected-run tabs call the scoped query contract.

## SB02-I03 Cached Input Appears In Analytics Statistics

- Raw note: provider cached tokens must be calculated correctly where provider supports them.
- Expected behavior: cached input tokens are included in live stats and metric points and charted only when non-zero data exists.
- Disallowed shallow implementation: persist cached tokens but omit them from historical aggregates.
- Failing-first proof: source assertion proves the aggregator sums cached input into stats and metric point accumulators.
- Passing proof: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Red-team negative case: providers without cached data leave the cached series absent because the UI adds the series only when any point has cached tokens.
- Downstream dependency check: SB03 shared graph panel uses the same cached-input metric points.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessLiveObservationQuery.ProcessDefinitionId` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | created by process workspace graph tab on explicit load | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt` |
| `ProcessLiveObservationQuery.ProcessRunId` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | created by selected-run graph tab on tab activation | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt` |
| Completed run history inclusion | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | live dashboard and process graph panels consume metric points | completed run updated inside history window remains visible after refresh | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt` |
