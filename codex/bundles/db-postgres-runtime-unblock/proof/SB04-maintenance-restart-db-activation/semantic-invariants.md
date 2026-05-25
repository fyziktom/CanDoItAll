# SB04 Semantic Invariants

## Invariants

### SB04-I1 Profile activation is restart-first by default

Raw note: "Persisting a new active profile should require restart by default."

Expected behavior: activating a different persisted PostgreSQL profile updates the active profile record and returns `RequiresRestart=true` while keeping `RuntimeChangedInProcess=false`.

Shallow-pass trap: change UI copy but still hot-switch the running process under the covers.

Adversarial negative proof: Playwright switch proof verifies stale artifact/cross-tab runtime state stays on the running canonical profile until restart.

Semantic positive proof: `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` restart-first coordinator and API/UI DTOs.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

Changed source files: see `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`.

Downstream dependency check: SB07 transfer/admin boundaries rely on activation not changing runtime in-process.

### SB04-I2 UI and API expose activated-vs-runtime distinction

Raw note: "Restart/maintenance semantics are visible in UI/API/tests."

Expected behavior: Data Sources and cognitive memory endpoints return restart-required activation messages and separate runtime profile from activated profile.

Shallow-pass trap: add backend result flags without rendering or API surfacing.

Adversarial negative proof: component tests assert restart text and Playwright screenshots capture actual rendered state.

Semantic positive proof: `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-components-database-sources.txt` and `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt`.

Production assertions: `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`, `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.DatabaseEndpoints.cs`, and `repo://src/CanDoItAll.Web/Program.cs`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `RequiresRestart` activation result | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor` | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-playwright-database-switch.txt` | `bundle://proof/SB04-maintenance-restart-db-activation/transcripts/dotnet-test-components-database-sources.txt` |
