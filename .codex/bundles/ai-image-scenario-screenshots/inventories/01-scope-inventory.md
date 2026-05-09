# Scope Inventory

## Scenario Inventory

| Scenario | App | Technology | Source root | Capture target |
| --- | --- | --- | --- | --- |
| `scenario-01` | Trailhead Snack Box Inventory | .NET Razor Pages | `C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box` | `/inventory` |
| `scenario-02` | Tool Calibration Log | .NET Blazor | `C:\programovani\candoitall-dev-55-output\scenario-02-dotnet-tool-calibration-log` | `/`, `/calibrations`, `/calibrations/new`, `/calibrations/{RecordId}` |
| `scenario-03` | Rain Barrel Chore Splitter | JavaScript/Vite | `C:\programovani\candoitall-dev-55-output\scenario-03-js-rain-barrel-chore-splitter` | `/` |

## App Startup Expectations

| Scenario | Expected command family | Notes |
| --- | --- | --- |
| `scenario-01` | `dotnet run --project src/TrailheadSnackBox.Web` | README says run web host from `src/TrailheadSnackBox.Web` and open `/inventory`. |
| `scenario-02` | `dotnet run` from scenario root | Root contains Blazor app `Program.cs` and `.csproj`. |
| `scenario-03` | `npm run dev` or `npm run preview` | Vite package includes `dev`, `build`, `preview`, and `test`. |

## Existing Extension Points

| Surface | Existing file | Planned use |
| --- | --- | --- |
| Provider profile models | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs` | Add image-provider profile semantics. |
| Provider kind enum | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Common\Enums.cs` | Add typed image provider kind if required. |
| Agent definition | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs` | Preserve agent identity while adding config metadata. |
| Agent tool metadata | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentWorkspaceToolAccessModels.cs` | Follow existing read/write JSON metadata pattern for image access. |
| Seed catalog | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs` | Seed provider profiles, capabilities, and templates. |
| Process template pack | `C:\repositories\CanDoItAll\Templates\Processes\manifest.json` | Add screenshot and layout processes. |
| Project structure API | `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs` | Create/read projects, nodes, process nodes, assets. |
