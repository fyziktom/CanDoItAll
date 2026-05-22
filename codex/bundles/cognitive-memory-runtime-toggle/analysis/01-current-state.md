# Current State

## Relevant Repo State

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` registers `CognitiveMemoryAgentContextContributor` through `IAgentContextContributor`. It reads automation settings, evaluates model access, then requires a project scope before calling recall.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs` turns contributor `Failed` results into `AgentContextContributionException`, which matches the user's stack trace.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs` has automation and model access settings, including `CognitiveMemoryModelAccessMode.Disabled`, but no separate usage flag.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntities.cs` persists settings in `CognitiveMemory_AutomationSettings`.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` can automatically ingest sources and consolidate memory from scheduled or manual automation triggers.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` also provides Cognitive Memory workflow executors for recall, probe, and learning proposals.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemorySettingsTab.razor` already contains the settings UI and is the smallest visible place to expose runtime on/off.
- `repo://src/CanDoItAll.Web/appsettings.Development.json` points development PostgreSQL to `Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall`.

## Integration Points To Gate

| Surface | Why it is connected to other parts | Required disabled behavior |
| --- | --- | --- |
| Agent context contributor | Injects memory into general MAF agent calls. | Return `Skipped` before model access, project scope, or recall. |
| Cognitive Memory workflow executors | Workflow nodes can run inside general workflow/process demos. | Return deterministic skipped JSON payloads without validating project-specific executor settings. |
| Scheduled automation runner | Can run from background/manual automation and mutate memory stores. | Return `Executed = false` with a disabled warning before ingestion/consolidation. |
| Settings API/UI | Runtime control surface. | Persist and display `IsEnabled` without app restart. |

## Out Of Scope Direct Surfaces

- Cognitive Memory status, database profile selection, and settings endpoints must stay available while disabled.
- Direct Cognitive Memory management screens can still load; action buttons may show disabled state if implementation can do so with small edits.
